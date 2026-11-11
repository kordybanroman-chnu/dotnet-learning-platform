using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Events;
using RabbitMQ.Client;

namespace Platform.EventBus;

public sealed class RabbitMqEventPublisher(
    IConnection connection,
    ILogger<RabbitMqEventPublisher> logger) : IEventPublisher
{
    public async Task PublishAsync<T>(
        T @event, string exchange, string routingKey, CancellationToken ct = default)
        where T : IntegrationEvent
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(@event, EventBusJson.Options);
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = @event.EventId.ToString(),
            Type = typeof(T).Name,
            CorrelationId = @event.CorrelationId,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>
            {
                ["x-correlation-id"] = @event.CorrelationId,
                ["x-event-type"] = typeof(T).Name,
                ["x-schema-version"] = 1
            }
        };
        RabbitMqTrace.Inject(properties);

        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await channel.BasicPublishAsync(exchange, routingKey, false, properties, body, ct);

        logger.LogInformation(
            "Published {EventType} {EventId} to {Exchange}/{RoutingKey} CorrelationId {CorrelationId}",
            typeof(T).Name, @event.EventId, exchange, routingKey, @event.CorrelationId);
    }
}
