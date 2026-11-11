using System.Diagnostics.Metrics;
using Catalog.Dal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Catalog.Api.Metrics;

public sealed class CatalogMetrics
{
    public const string MeterName = "Catalog.Api";

    private readonly Counter<long> _created;
    private readonly Counter<long> _priceUpdated;
    private readonly Counter<long> _deleted;
    private readonly Histogram<double> _duration;
    private long _coursesTotal;

    public CatalogMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _created = meter.CreateCounter<long>(
            "catalog.courses.created", unit: "{course}", description: "Courses created");
        _priceUpdated = meter.CreateCounter<long>(
            "catalog.courses.price_updated", unit: "{course}", description: "Course prices updated");
        _deleted = meter.CreateCounter<long>(
            "catalog.courses.deleted", unit: "{course}", description: "Courses deleted");
        _duration = meter.CreateHistogram<double>(
            "catalog.operation.duration", unit: "ms", description: "Catalog operation duration");
        meter.CreateObservableGauge(
            "catalog.courses.total",
            () => Volatile.Read(ref _coursesTotal),
            unit: "{course}",
            description: "Courses in the catalog");
    }

    public void CourseCreated(bool hasDetail, int categoryCount) =>
        _created.Add(
            1,
            new KeyValuePair<string, object?>("has.detail", hasDetail ? "yes" : "no"),
            new KeyValuePair<string, object?>("category.bucket", CategoryBucket(categoryCount)));

    public void PriceUpdated(decimal price) =>
        _priceUpdated.Add(
            1,
            new KeyValuePair<string, object?>("price.band", PriceBand(price)));

    public void CourseDeleted() =>
        _deleted.Add(1, new KeyValuePair<string, object?>("source", "api"));

    public void RecordDuration(string operation, bool success, double milliseconds) =>
        _duration.Record(
            milliseconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("result", success ? "success" : "failure"));

    public void SetCourseCount(long value) =>
        Interlocked.Exchange(ref _coursesTotal, value);

    private static string CategoryBucket(int count) => count switch
    {
        0 => "0",
        1 => "1",
        _ => "2+"
    };

    private static string PriceBand(decimal price) => price switch
    {
        < 500m => "low",
        < 1500m => "medium",
        _ => "high"
    };
}

public sealed class CatalogMetricsPoller(
    IServiceScopeFactory scopeFactory,
    CatalogMetrics metrics,
    ILogger<CatalogMetricsPoller> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
                var count = await db.Courses.CountAsync(stoppingToken);
                metrics.SetCourseCount(count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to refresh catalog metrics");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
