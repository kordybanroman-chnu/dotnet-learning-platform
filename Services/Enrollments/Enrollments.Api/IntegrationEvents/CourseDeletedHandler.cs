using Enrollments.Dal;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Events;

namespace Enrollments.Api.IntegrationEvents;

public sealed class CourseDeletedHandler(
    IUnitOfWork uow,
    ILogger<CourseDeletedHandler> logger)
    : CatalogEventHandler<CourseDeletedEvent>(uow, logger)
{
    protected override async Task ApplyAsync(CourseDeletedEvent @event, CancellationToken ct)
    {
        var affected = await Uow.Courses.DeleteSnapshotAsync(
            @event.CourseId, @event.OccurredAt, ct);
        if (affected == 0)
            logger.LogWarning(
                "Course {CourseId} was retained or already missing for {EventType} {EventId}: stale event or existing enrollment items",
                @event.CourseId, nameof(CourseDeletedEvent), @event.EventId);
    }

    protected override void LogApplied(CourseDeletedEvent @event)
        => logger.LogInformation("Local course {CourseId} removed", @event.CourseId);
}
