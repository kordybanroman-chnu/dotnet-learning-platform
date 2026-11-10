using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Infrastructure.Persistence;

namespace Reviews.WebApi.Metrics;

public sealed class ReviewMetrics
{
    public const string MeterName = "Reviews.WebApi";

    private readonly Counter<long> _created;
    private readonly Counter<long> _updated;
    private readonly Counter<long> _deleted;
    private readonly Histogram<double> _duration;
    private long _reviewsTotal;

    public ReviewMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _created = meter.CreateCounter<long>(
            "reviews.created", unit: "{review}", description: "Reviews created");
        _updated = meter.CreateCounter<long>(
            "reviews.updated", unit: "{review}", description: "Reviews updated");
        _deleted = meter.CreateCounter<long>(
            "reviews.deleted", unit: "{review}", description: "Reviews deleted");
        _duration = meter.CreateHistogram<double>(
            "reviews.operation.duration", unit: "ms", description: "Review operation duration");
        meter.CreateObservableGauge(
            "reviews.total",
            () => Volatile.Read(ref _reviewsTotal),
            unit: "{review}",
            description: "Reviews stored in MongoDB");
    }

    public void Created(int rating) =>
        _created.Add(1, new KeyValuePair<string, object?>("rating", rating));

    public void Updated(int rating) =>
        _updated.Add(1, new KeyValuePair<string, object?>("rating", rating));

    public void Deleted() =>
        _deleted.Add(1, new KeyValuePair<string, object?>("source", "api"));

    public void RecordDuration(string operation, bool success, double milliseconds) =>
        _duration.Record(
            milliseconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("result", success ? "success" : "failure"));

    public void SetReviewCount(long value) =>
        Interlocked.Exchange(ref _reviewsTotal, value);
}

public sealed class ReviewMetricsPoller(
    IServiceScopeFactory scopeFactory,
    ReviewMetrics metrics,
    ILogger<ReviewMetricsPoller> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
                var count = await context.Reviews.CountDocumentsAsync(
                    FilterDefinition<Review>.Empty, cancellationToken: stoppingToken);
                metrics.SetReviewCount(count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to refresh review metrics");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
