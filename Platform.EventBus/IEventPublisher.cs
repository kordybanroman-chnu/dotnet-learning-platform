using Platform.EventBus.Events;

namespace Platform.EventBus;

public interface IEventPublisher
{
    Task PublishAsync<T>(
        T @event, string exchange, string routingKey, CancellationToken ct = default)
        where T : IntegrationEvent;
}
