using System.Diagnostics;
using System.Text.Json;
using Catalog.Grpc;
using Enrollments.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Caching.Distributed;
using Platform.Aggregator.Models;
using Reviews.Grpc;

namespace Platform.Aggregator.Services;

public sealed class EnrollmentAggregator(
    EnrollmentsService.EnrollmentsServiceClient enrollments,
    CatalogService.CatalogServiceClient catalog,
    ReviewsService.ReviewsServiceClient reviews,
    IDistributedCache cache,
    ILogger<EnrollmentAggregator> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public async Task<FullEnrollmentDto?> GetFullEnrollmentAsync(long enrollmentId, CancellationToken ct)
    {
        var cacheKey = $"aggregator:enrollment:{enrollmentId}:full";
        var cached = await TryGetAsync(cacheKey, ct);
        if (cached is not null)
        {
            logger.LogInformation("Cache hit {Key}", cacheKey);
            return cached;
        }

        logger.LogDebug("Cache miss {Key}", cacheKey);

        EnrollmentReply enrollment;
        try
        {
            enrollment = await enrollments.GetEnrollmentAsync(
                new GetEnrollmentRequest { Id = enrollmentId }, cancellationToken: ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }

        var courseIds = enrollment.Items.Select(item => item.CourseId).Distinct().ToList();
        var warnings = new List<string>();

        var coursesTask = SafeAsync(
            () => catalog.GetCoursesByIdsAsync(
                new GetCoursesByIdsRequest { Ids = { courseIds } },
                cancellationToken: ct).ResponseAsync,
            "catalog-api", warnings);

        var reviewsTask = SafeAsync(
            () => reviews.GetForCoursesAsync(
                new GetForCoursesRequest { CourseIds = { courseIds.Select(id => (int)id) } },
                cancellationToken: ct).ResponseAsync,
            "reviews-api", warnings);

        await Task.WhenAll(coursesTask, reviewsTask);

        var result = new FullEnrollmentDto(
            ToDto(enrollment),
            (await coursesTask)?.Courses.Select(ToDto).ToList() ?? [],
            (await reviewsTask)?.Reviews.Select(ToDto).ToList() ?? [],
            warnings);

        if (warnings.Count == 0)
            await TrySetAsync(cacheKey, result, ct);

        return result;
    }

    private async Task<T?> SafeAsync<T>(
        Func<Task<T>> call, string service, List<string> warnings) where T : class
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await call();
            logger.LogInformation("{Service} responded in {ElapsedMilliseconds} ms",
                service, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            logger.LogWarning(ex, "{Service} is unavailable after {ElapsedMilliseconds} ms",
                service, stopwatch.ElapsedMilliseconds);
            lock (warnings)
                warnings.Add($"{service} is unavailable");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Service} failed after {ElapsedMilliseconds} ms",
                service, stopwatch.ElapsedMilliseconds);
            lock (warnings)
                warnings.Add($"{service} is unavailable");
            return null;
        }
    }

    private async Task<FullEnrollmentDto?> TryGetAsync(string key, CancellationToken ct)
    {
        try
        {
            var bytes = await cache.GetAsync(key, ct);
            return bytes is null ? null : JsonSerializer.Deserialize<FullEnrollmentDto>(bytes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Redis read failed for {Key}", key);
            return null;
        }
    }

    private async Task TrySetAsync(string key, FullEnrollmentDto value, CancellationToken ct)
    {
        try
        {
            await cache.SetAsync(
                key,
                JsonSerializer.SerializeToUtf8Bytes(value),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Redis write failed for {Key}", key);
        }
    }

    private static EnrollmentDto ToDto(EnrollmentReply enrollment) => new(
        enrollment.Id,
        enrollment.StudentId,
        enrollment.Status,
        ToDateTime(enrollment.EnrolledAt),
        string.IsNullOrEmpty(enrollment.Note) ? null : enrollment.Note,
        string.IsNullOrEmpty(enrollment.PreferredSchedule) ? null : enrollment.PreferredSchedule,
        enrollment.Items
            .Select(item => new EnrollmentItemDto(
                item.CourseId, item.CourseTitle, (decimal)item.UnitPrice, item.Units))
            .ToList());

    private static CourseDto ToDto(CourseReply course) => new(
        course.Id,
        course.Slug,
        course.Title,
        (decimal)course.Price,
        course.InstructorId,
        course.InstructorName,
        course.Detail is null
            ? null
            : new CourseDetailDto(course.Detail.Syllabus, course.Detail.DurationHours),
        course.Images
            .Select(image => new CourseImageDto(image.Id, image.Url, image.SortOrder))
            .ToList(),
        course.Categories.ToList(),
        ToDateTime(course.CreatedAt));

    private static ReviewDto ToDto(ReviewReply review) => new(
        review.Id,
        review.CourseId,
        review.CourseTitle,
        review.AuthorId,
        review.AuthorName,
        review.Rating,
        review.Text,
        review.Photos.ToList(),
        review.Comments
            .Select(comment => new CommentDto(
                string.IsNullOrEmpty(comment.Id) ? null : comment.Id,
                comment.Author,
                comment.Text,
                ToDateTime(comment.CreatedAt),
                comment.Version))
            .ToList(),
        review.CommentsCount,
        string.IsNullOrEmpty(review.DiscussionId) ? null : review.DiscussionId,
        review.Version,
        ToDateTime(review.CreatedAt),
        review.UpdatedAt is null ? null : review.UpdatedAt.ToDateTime());

    private static DateTime ToDateTime(Timestamp? value) =>
        value is null ? DateTime.UnixEpoch : value.ToDateTime();
}
