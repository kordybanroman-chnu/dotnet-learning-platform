using System.Diagnostics;
using AutoMapper;
using Catalog.Bll.Caching;
using Catalog.Bll.Specifications;
using Catalog.Dal;
using Catalog.Domain;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Platform.EventBus;
using Platform.EventBus.Events;

namespace Catalog.Bll;

public interface ICourseService
{
    Task<PagedResult<CourseListItemDto>> GetListAsync(CourseQuery query, CancellationToken ct = default);
    Task<CourseDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<CourseListItemDto>> GetByInstructorAsync(long instructorId, CancellationToken ct = default);
    Task<IReadOnlyList<CourseShortDto>> GetRecentlyAddedAsync(
        string categorySlug, DateTime since, CancellationToken ct = default);
    Task<CourseDto> CreateAsync(CreateCourseDto input, CancellationToken ct = default);
    Task UpdatePriceAsync(long id, decimal price, CancellationToken ct = default);
    Task UpdateTitleAsync(long id, string title, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public sealed class CourseService(
    IUnitOfWork uow,
    IMapper mapper,
    ICatalogCache cache,
    IEventPublisher publisher,
    ICorrelationAccessor correlation,
    ILogger<CourseService> logger) : ICourseService
{
    private static readonly ActivitySource ActivitySource = new("Catalog.Api");

    public async Task<PagedResult<CourseListItemDto>> GetListAsync(
        CourseQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await uow.Courses.ListAsync(new CourseFilterSpec(query, page, pageSize), ct);
        var total = await uow.Courses.CountAsync(new CourseFilterSpec(query, page, pageSize, paged: false), ct);

        return new PagedResult<CourseListItemDto>(
            mapper.Map<IReadOnlyList<CourseListItemDto>>(items), total, page, pageSize);
    }

    public Task<CourseDto> GetByIdAsync(long id, CancellationToken ct = default)
        => cache.GetOrCreateAsync(
            CacheKeys.Course(id),
            async () =>
            {
                var course = await uow.Courses.GetWithDetailsAsync(id, ct)
                    ?? throw new NotFoundException($"Course {id} not found");
                return mapper.Map<CourseDto>(course);
            },
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(10),
            ct);

    public Task<IReadOnlyList<CourseListItemDto>> GetByInstructorAsync(
        long instructorId, CancellationToken ct = default)
        => cache.GetOrCreateAsync(
            CacheKeys.InstructorCourses(instructorId),
            async () => mapper.Map<IReadOnlyList<CourseListItemDto>>(
                await uow.Courses.ListAsync(new CoursesByInstructorSpec(instructorId), ct)),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            ct);

    public async Task<IReadOnlyList<CourseShortDto>> GetRecentlyAddedAsync(
        string categorySlug, DateTime since, CancellationToken ct = default)
    {
        var courses = await uow.CourseCategories.GetRecentlyAddedAsync(categorySlug, since, ct);
        return mapper.Map<IReadOnlyList<CourseShortDto>>(courses);
    }

    public async Task<CourseDto> CreateAsync(CreateCourseDto input, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("CreateCourse");
        activity?.SetTag("course.slug", input.Slug);

        if (await uow.Courses.ExistsBySlugAsync(input.Slug, ct))
            throw new ConflictException($"Course with slug '{input.Slug}' already exists");

        if (await uow.Instructors.GetByIdAsync(input.InstructorId, ct) is null)
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(input.InstructorId), $"Instructor {input.InstructorId} not found")
            });

        var categoryIds = input.CategoryIds.Distinct().ToList();
        var missing = await uow.Categories.GetMissingIdsAsync(categoryIds, ct);
        if (missing.Count > 0)
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(input.CategoryIds),
                    $"Unknown categories: {string.Join(", ", missing)}")
            });

        var course = new Course(input.Slug, input.Title, input.Price, input.InstructorId);

        if (!string.IsNullOrWhiteSpace(input.Syllabus))
        {
            if (input.DurationHours is null or <= 0)
                throw new DomainException("Duration must be positive when syllabus is provided");
            course.SetDetail(input.Syllabus, input.DurationHours.Value);
        }

        foreach (var categoryId in categoryIds)
            course.AttachToCategory(categoryId);

        if (!string.IsNullOrWhiteSpace(input.ImageUrl))
            course.AddImage(input.ImageUrl, 0);

        await uow.Courses.AddAsync(course, ct);
        await uow.SaveChangesAsync(ct);

        activity?.SetTag("course.id", course.Id);
        logger.LogInformation("Created course {CourseId} {Slug} with price {Price}",
            course.Id, course.Slug, course.Price);

        await cache.InvalidateAsync(
            CacheKeys.InstructorCourses(course.InstructorId), "CourseCreated", ct);

        return await GetByIdAsync(course.Id, ct);
    }

    public async Task UpdatePriceAsync(long id, decimal price, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("UpdateCoursePrice");
        activity?.SetTag("course.id", id);
        activity?.SetTag("course.price", price);

        var course = await uow.Courses.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Course {id} not found");
        var oldPrice = course.Price;
        course.ChangePrice(price);
        await uow.SaveChangesAsync(ct);

        await cache.InvalidateAsync(CacheKeys.Course(id), "CoursePriceUpdated", ct);
        await cache.InvalidateAsync(
            CacheKeys.InstructorCourses(course.InstructorId), "CoursePriceUpdated", ct);

        await publisher.PublishAsync(
            new CoursePriceChangedEvent(id, course.Title, oldPrice, price)
            {
                CorrelationId = correlation.CorrelationId
            },
            RabbitMqTopology.CatalogExchange,
            CourseRoutingKeys.CoursePriceChanged,
            ct);

        logger.LogInformation("Updated course {CourseId} price to {Price}", id, price);
    }

    public async Task UpdateTitleAsync(long id, string title, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("UpdateCourseTitle");
        activity?.SetTag("course.id", id);

        var course = await uow.Courses.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Course {id} not found");
        var oldTitle = course.Title;
        course.ChangeTitle(title);
        await uow.SaveChangesAsync(ct);

        await cache.InvalidateAsync(CacheKeys.Course(id), "CourseRenamed", ct);
        await cache.InvalidateAsync(
            CacheKeys.InstructorCourses(course.InstructorId), "CourseRenamed", ct);

        await publisher.PublishAsync(
            new CourseRenamedEvent(id, oldTitle, course.Title)
            {
                CorrelationId = correlation.CorrelationId
            },
            RabbitMqTopology.CatalogExchange,
            CourseRoutingKeys.CourseRenamed,
            ct);

        logger.LogInformation("Renamed course {CourseId} from {OldTitle} to {NewTitle}",
            id, oldTitle, course.Title);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("DeleteCourse");
        activity?.SetTag("course.id", id);

        var course = await uow.Courses.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Course {id} not found");
        uow.Courses.Delete(course);
        await uow.SaveChangesAsync(ct);

        await cache.InvalidateAsync(CacheKeys.Course(id), "CourseDeleted", ct);
        await cache.InvalidateAsync(
            CacheKeys.InstructorCourses(course.InstructorId), "CourseDeleted", ct);

        logger.LogInformation("Deleted course {CourseId}", id);
    }
}
