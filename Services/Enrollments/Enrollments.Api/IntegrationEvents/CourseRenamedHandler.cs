using Enrollments.Dal;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Events;

namespace Enrollments.Api.IntegrationEvents;

public sealed class CourseRenamedHandler(
    IUnitOfWork uow,
    ILogger<CourseRenamedHandler> logger)
{
    public async Task HandleAsync(CourseRenamedEvent @event, CancellationToken ct)
    {
        await uow.BeginTransactionAsync(ct: ct);
        try
        {
            if (!await uow.ProcessedEvents.TryAddAsync(
                    @event.EventId, nameof(CourseRenamedEvent), ct))
            {
                await uow.RollbackAsync(ct);
                logger.LogInformation(
                    "Duplicate {EventType} {EventId} for course {CourseId} skipped",
                    nameof(CourseRenamedEvent), @event.EventId, @event.CourseId);
                return;
            }

            var affected = await uow.Courses.UpdateSnapshotAsync(
                @event.CourseId, @event.NewTitle, null, ct);
            if (affected == 0)
                logger.LogWarning(
                    "Course {CourseId} from {EventType} {EventId} is missing in the local catalog copy",
                    @event.CourseId, nameof(CourseRenamedEvent), @event.EventId);

            await uow.CommitAsync(ct);
            logger.LogInformation(
                "Applied {EventType} {EventId} to course {CourseId}: title {OldTitle} -> {NewTitle}",
                nameof(CourseRenamedEvent), @event.EventId, @event.CourseId,
                @event.OldTitle, @event.NewTitle);
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }
    }
}
