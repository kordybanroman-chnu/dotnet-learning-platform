using Enrollments.Dal;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Events;

namespace Enrollments.Api.IntegrationEvents;

public sealed class CoursePriceChangedHandler(
    IUnitOfWork uow,
    ILogger<CoursePriceChangedHandler> logger)
{
    public async Task HandleAsync(CoursePriceChangedEvent @event, CancellationToken ct)
    {
        await uow.BeginTransactionAsync(ct: ct);
        try
        {
            if (!await uow.ProcessedEvents.TryAddAsync(
                    @event.EventId, nameof(CoursePriceChangedEvent), ct))
            {
                await uow.RollbackAsync(ct);
                logger.LogInformation(
                    "Duplicate {EventType} {EventId} for course {CourseId} skipped",
                    nameof(CoursePriceChangedEvent), @event.EventId, @event.CourseId);
                return;
            }

            var affected = await uow.Courses.UpdateSnapshotAsync(
                @event.CourseId, @event.Title, @event.NewPrice, ct);
            if (affected == 0)
                logger.LogWarning(
                    "Course {CourseId} from {EventType} {EventId} is missing in the local catalog copy",
                    @event.CourseId, nameof(CoursePriceChangedEvent), @event.EventId);

            await uow.CommitAsync(ct);
            logger.LogInformation(
                "Applied {EventType} {EventId} to course {CourseId}: price {OldPrice} -> {NewPrice}",
                nameof(CoursePriceChangedEvent), @event.EventId, @event.CourseId,
                @event.OldPrice, @event.NewPrice);
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }
    }
}
