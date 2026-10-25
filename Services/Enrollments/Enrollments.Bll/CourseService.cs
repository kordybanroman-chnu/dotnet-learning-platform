using AutoMapper;
using Enrollments.Dal;
using Enrollments.Domain;

namespace Enrollments.Bll;

public interface ICourseService
{
    Task<CourseDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<CourseDto>> GetAllAsync(CancellationToken ct = default);
}

public sealed class CourseService(IUnitOfWork uow, IMapper mapper) : ICourseService
{
    public async Task<CourseDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var course = await uow.Courses.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Course {id} not found");
        return mapper.Map<CourseDto>(course);
    }

    public async Task<IReadOnlyList<CourseDto>> GetAllAsync(CancellationToken ct = default)
    {
        var courses = await uow.Courses.GetAllAsync(ct);
        return mapper.Map<IReadOnlyList<CourseDto>>(courses);
    }
}
