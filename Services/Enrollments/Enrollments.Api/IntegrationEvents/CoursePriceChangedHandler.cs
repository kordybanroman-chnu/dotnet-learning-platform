using Enrollments.Dal;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Events;

namespace Enrollments.Api.IntegrationEvents;

public sealed class CoursePriceChangedHandler(
    IUnitOfWork uow,
    ILogger<CoursePriceChangedHandler> logger)
    : CatalogEventHandler<CoursePriceChangedEvent>(uow, logger)
{
    protected override async Task ApplyAsync(CoursePriceChangedEvent @event, CancellationToken ct)
    {
        var affected = await Uow.Courses.UpdateSnapshotAsync(
            @event.CourseId, @event.Title, @event.NewPrice, @event.OccurredAt, ct);
        if (affected == 0)
            logger.LogWarning(
                "Course {CourseId} was not updated by {EventType} {EventId}: stale event or missing local copy",
                @event.CourseId, nameof(CoursePriceChangedEvent), @event.EventId);
    }

    protected override void LogApplied(CoursePriceChangedEvent @event)
        => logger.LogInformation(
            "Course {CourseId} price changed {OldPrice} -> {NewPrice}",
            @event.CourseId, @event.OldPrice, @event.NewPrice);
}
