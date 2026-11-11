using Enrollments.Dal;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Events;

namespace Enrollments.Api.IntegrationEvents;

public abstract class CatalogEventHandler<TEvent>(IUnitOfWork uow, ILogger logger)
    where TEvent : IntegrationEvent
{
    protected IUnitOfWork Uow { get; } = uow;

    public async Task HandleAsync(TEvent @event, CancellationToken ct)
    {
        await Uow.BeginTransactionAsync(ct: ct);
        try
        {
            if (!await Uow.ProcessedEvents.TryAddAsync(
                    @event.EventId, typeof(TEvent).Name, ct))
            {
                await Uow.RollbackAsync(ct);
                logger.LogInformation(
                    "Duplicate {EventType} {EventId} skipped",
                    typeof(TEvent).Name, @event.EventId);
                return;
            }

            await ApplyAsync(@event, ct);
            await Uow.CommitAsync(ct);
            LogApplied(@event);
            logger.LogInformation(
                "Applied {EventType} {EventId} CorrelationId {CorrelationId}",
                typeof(TEvent).Name, @event.EventId, @event.CorrelationId);
        }
        catch
        {
            await Uow.RollbackAsync(ct);
            throw;
        }
    }

    protected abstract Task ApplyAsync(TEvent @event, CancellationToken ct);

    protected virtual void LogApplied(TEvent @event)
    {
    }
}
