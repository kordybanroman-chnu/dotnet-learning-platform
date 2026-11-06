using System.Data;
using System.Diagnostics;
using AutoMapper;
using Enrollments.Dal;
using Enrollments.Domain;
using Microsoft.Extensions.Logging;

namespace Enrollments.Bll;

public interface IEnrollmentService
{
    Task<EnrollmentDto> CreateAsync(CreateEnrollmentDto input, CancellationToken ct = default);
    Task<EnrollmentDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<EnrollmentDto>> GetByStudentAsync(long studentId, CancellationToken ct = default);
    Task ConfirmAsync(long id, CancellationToken ct = default);
    Task CancelAsync(long id, CancellationToken ct = default);
}

public sealed class EnrollmentService(IUnitOfWork uow, IMapper mapper, ILogger<EnrollmentService> logger) : IEnrollmentService
{
    private static readonly ActivitySource ActivitySource = new("Enrollments.Api");

    public async Task<EnrollmentDto> CreateAsync(CreateEnrollmentDto input, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("CreateEnrollment");
        activity?.SetTag("student.id", input.StudentId);

        foreach (var item in input.Items)
            if (item.Units <= 0)
                throw new ValidationException("Units must be positive");
        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var id = await uow.Enrollments.AddAsync(new Enrollment
            {
                StudentId = input.StudentId,
                Note = input.Note,
                PreferredSchedule = input.PreferredSchedule,
            }, ct);
            foreach (var item in input.Items)
            {
                _ = await uow.Courses.GetByIdAsync(item.CourseId, ct)
                    ?? throw new NotFoundException($"Course {item.CourseId} not found");
                await uow.Enrollments.AddItemAsync(id, item.CourseId, item.Units, ct);
            }
            await uow.CommitAsync(ct);
            activity?.SetTag("enrollment.id", id);
            logger.LogInformation("Created enrollment {EnrollmentId} for student {StudentId}", id, input.StudentId);
            var created = await uow.Enrollments.GetWithItemsAsync(id, ct)
                ?? throw new NotFoundException($"Enrollment {id} not found");
            return mapper.Map<EnrollmentDto>(created);
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<EnrollmentDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var enrollment = await uow.Enrollments.GetWithItemsAsync(id, ct)
            ?? throw new NotFoundException($"Enrollment {id} not found");
        return mapper.Map<EnrollmentDto>(enrollment);
    }

    public async Task<IReadOnlyList<EnrollmentDto>> GetByStudentAsync(long studentId, CancellationToken ct = default)
    {
        var enrollments = await uow.Enrollments.GetByStudentAsync(studentId, ct);
        return mapper.Map<IReadOnlyList<EnrollmentDto>>(enrollments);
    }

    public async Task ConfirmAsync(long id, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("ConfirmEnrollment");
        activity?.SetTag("enrollment.id", id);

        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var enrollment = await uow.Enrollments.GetWithItemsAsync(id, ct)
                ?? throw new NotFoundException($"Enrollment {id} not found");
            if (enrollment.Items.Count == 0)
                throw new BusinessConflictException($"Enrollment {id} has no items");
            await uow.Enrollments.ConfirmAsync(id, ct);
            await uow.CommitAsync(ct);
            logger.LogInformation("Confirmed enrollment {EnrollmentId}", id);
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task CancelAsync(long id, CancellationToken ct = default)
    {
        await uow.Enrollments.CancelAsync(id, ct);
        logger.LogInformation("Cancelled enrollment {EnrollmentId}", id);
    }
}
