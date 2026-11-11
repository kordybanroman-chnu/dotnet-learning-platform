using Enrollments.Dal;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Events;

namespace Enrollments.Api.IntegrationEvents;

public sealed class CourseCreatedHandler(
    IUnitOfWork uow,
    ILogger<CourseCreatedHandler> logger)
    : CatalogEventHandler<CourseCreatedEvent>(uow, logger)
{
    protected override async Task ApplyAsync(CourseCreatedEvent @event, CancellationToken ct)
    {
        var affected = await Uow.Courses.InsertSnapshotAsync(
            @event.CourseId, @event.Title, @event.Price, @event.OccurredAt, ct);
        if (affected == 0)
            logger.LogWarning(
                "Course {CourseId} was not inserted by {EventType} {EventId}: stale event",
                @event.CourseId, nameof(CourseCreatedEvent), @event.EventId);
    }

    protected override void LogApplied(CourseCreatedEvent @event)
        => logger.LogInformation(
            "Local course {CourseId} ready with title {Title} and price {Price}",
            @event.CourseId, @event.Title, @event.Price);
}
