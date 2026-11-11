using Enrollments.Dal;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Events;

namespace Enrollments.Api.IntegrationEvents;

public sealed class CourseRenamedHandler(
    IUnitOfWork uow,
    ILogger<CourseRenamedHandler> logger)
    : CatalogEventHandler<CourseRenamedEvent>(uow, logger)
{
    protected override async Task ApplyAsync(CourseRenamedEvent @event, CancellationToken ct)
    {
        var affected = await Uow.Courses.UpdateSnapshotAsync(
            @event.CourseId, @event.NewTitle, null, @event.OccurredAt, ct);
        if (affected == 0)
            logger.LogWarning(
                "Course {CourseId} was not updated by {EventType} {EventId}: stale event or missing local copy",
                @event.CourseId, nameof(CourseRenamedEvent), @event.EventId);
    }

    protected override void LogApplied(CourseRenamedEvent @event)
        => logger.LogInformation(
            "Course {CourseId} renamed {OldTitle} -> {NewTitle}",
            @event.CourseId, @event.OldTitle, @event.NewTitle);
}
