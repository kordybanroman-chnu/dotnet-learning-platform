using System.Diagnostics;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Services;

public sealed class EnrollmentAggregator(
    EnrollmentsClient enrollments,
    CatalogClient catalog,
    ReviewsClient reviews,
    ILogger<EnrollmentAggregator> logger)
{
    public async Task<FullEnrollmentDto?> GetFullEnrollmentAsync(long enrollmentId, CancellationToken ct)
    {
        logger.LogInformation("Loading enrollment {EnrollmentId} for aggregation", enrollmentId);

        var enrollment = await enrollments.GetEnrollmentAsync(enrollmentId, ct);
        if (enrollment is null)
            return null;

        var courseIds = enrollment.Items.Select(item => item.CourseId).Distinct().ToList();
        var warnings = new List<string>();

        var coursesTask = SafeAsync(
            () => catalog.GetCoursesAsync(courseIds, ct), "catalog-api", warnings, ct);
        var reviewsTask = SafeAsync(
            () => reviews.GetForCoursesAsync(courseIds, ct), "reviews-api", warnings, ct);

        await Task.WhenAll(coursesTask, reviewsTask);

        return new FullEnrollmentDto(
            enrollment,
            await coursesTask ?? [],
            await reviewsTask ?? [],
            warnings);
    }

    private async Task<IReadOnlyList<T>?> SafeAsync<T>(
        Func<Task<IReadOnlyList<T>>> call,
        string service,
        List<string> warnings,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            logger.LogInformation("Calling {Service}", service);
            var result = await call();
            stopwatch.Stop();
            logger.LogInformation("{Service} responded in {ElapsedMilliseconds} ms",
                service, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogWarning(ex, "{Service} is unavailable after {ElapsedMilliseconds} ms",
                service, stopwatch.ElapsedMilliseconds);
            lock (warnings)
                warnings.Add($"{service} is unavailable");
            return null;
        }
    }
}
