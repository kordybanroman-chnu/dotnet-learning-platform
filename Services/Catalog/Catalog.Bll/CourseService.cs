using AutoMapper;
using Catalog.Bll.Specifications;
using Catalog.Dal;
using Catalog.Domain;
using FluentValidation;
using FluentValidation.Results;

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
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public sealed class CourseService(IUnitOfWork uow, IMapper mapper) : ICourseService
{
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

    public async Task<CourseDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var course = await uow.Courses.GetWithDetailsAsync(id, ct)
            ?? throw new NotFoundException($"Course {id} not found");
        return mapper.Map<CourseDto>(course);
    }

    public async Task<IReadOnlyList<CourseListItemDto>> GetByInstructorAsync(
        long instructorId, CancellationToken ct = default)
    {
        var courses = await uow.Courses.ListAsync(new CoursesByInstructorSpec(instructorId), ct);
        return mapper.Map<IReadOnlyList<CourseListItemDto>>(courses);
    }

    public async Task<IReadOnlyList<CourseShortDto>> GetRecentlyAddedAsync(
        string categorySlug, DateTime since, CancellationToken ct = default)
    {
        var courses = await uow.CourseCategories.GetRecentlyAddedAsync(categorySlug, since, ct);
        return mapper.Map<IReadOnlyList<CourseShortDto>>(courses);
    }

    public async Task<CourseDto> CreateAsync(CreateCourseDto input, CancellationToken ct = default)
    {
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

        return await GetByIdAsync(course.Id, ct);
    }

    public async Task UpdatePriceAsync(long id, decimal price, CancellationToken ct = default)
    {
        var course = await uow.Courses.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Course {id} not found");
        course.ChangePrice(price);
        await uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var course = await uow.Courses.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Course {id} not found");
        uow.Courses.Delete(course);
        await uow.SaveChangesAsync(ct);
    }
}
