using System.Diagnostics.Metrics;
using Enrollments.Dal;
using Enrollments.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Enrollments.Api.Metrics;

public sealed class EnrollmentMetrics
{
    public const string MeterName = "Enrollments.Api";

    private readonly Counter<long> _created;
    private readonly Counter<long> _confirmed;
    private readonly Counter<long> _cancelled;
    private readonly Histogram<double> _duration;
    private long _awaitingValidation;

    public EnrollmentMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _created = meter.CreateCounter<long>(
            "enrollments.created", unit: "{enrollment}", description: "Enrollments created");
        _confirmed = meter.CreateCounter<long>(
            "enrollments.confirmed", unit: "{enrollment}", description: "Enrollments confirmed");
        _cancelled = meter.CreateCounter<long>(
            "enrollments.cancelled", unit: "{enrollment}", description: "Enrollments cancelled");
        _duration = meter.CreateHistogram<double>(
            "enrollments.operation.duration", unit: "ms", description: "Enrollment operation duration");
        meter.CreateObservableGauge(
            "enrollments.awaiting_validation",
            () => Volatile.Read(ref _awaitingValidation),
            unit: "{enrollment}",
            description: "Enrollments waiting for validation");
    }

    public void Created(string status) =>
        _created.Add(1, new KeyValuePair<string, object?>("status", status));

    public void Confirmed() =>
        _confirmed.Add(1, new KeyValuePair<string, object?>("status", EnrollmentStatus.SeatsConfirmed));

    public void Cancelled() =>
        _cancelled.Add(1, new KeyValuePair<string, object?>("status", EnrollmentStatus.Cancelled));

    public void RecordDuration(string operation, bool success, double milliseconds) =>
        _duration.Record(
            milliseconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("result", success ? "success" : "failure"));

    public void SetAwaitingValidation(long value) =>
        Interlocked.Exchange(ref _awaitingValidation, value);
}

public sealed class EnrollmentMetricsPoller(
    IServiceScopeFactory scopeFactory,
    EnrollmentMetrics metrics,
    ILogger<EnrollmentMetricsPoller> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var count = await uow.Enrollments.CountByStatusAsync(
                    EnrollmentStatus.AwaitingValidation, stoppingToken);
                metrics.SetAwaitingValidation(count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to refresh enrollment metrics");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
