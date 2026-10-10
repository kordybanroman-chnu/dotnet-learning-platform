using AutoMapper;
using Enrollments.Dal;
using Enrollments.Domain;

namespace Enrollments.Bll;

public interface IStudentService
{
    Task<StudentDto> CreateAsync(CreateStudentDto input, CancellationToken ct = default);
    Task<StudentDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<StudentDto>> GetAllAsync(CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public sealed class StudentService(IUnitOfWork uow, IMapper mapper) : IStudentService
{
    public async Task<StudentDto> CreateAsync(CreateStudentDto input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.FullName))
            throw new ValidationException("Email and FullName are required");
        var id = await uow.Students.AddAsync(new Student { Email = input.Email, FullName = input.FullName }, ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<StudentDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var student = await uow.Students.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Student {id} not found");
        return mapper.Map<StudentDto>(student);
    }

    public async Task<IReadOnlyList<StudentDto>> GetAllAsync(CancellationToken ct = default)
    {
        var students = await uow.Students.GetAllAsync(ct);
        return mapper.Map<IReadOnlyList<StudentDto>>(students);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var deleted = await uow.Students.DeleteAsync(id, ct);
        if (!deleted)
            throw new NotFoundException($"Student {id} not found");
    }
}
