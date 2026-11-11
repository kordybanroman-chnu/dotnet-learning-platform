using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.EventBus;
using Platform.EventBus.Events;
using RabbitMQ.Client;

namespace Reviews.WebApi.IntegrationEvents;

public sealed class CatalogEventsConsumer(
    IConnection connection,
    IServiceScopeFactory scopes,
    ILogger<CatalogEventsConsumer> logger)
    : RabbitMqEventConsumer(connection, scopes, logger)
{
    protected override string QueueName => RabbitMqTopology.ReviewsQueue;

    protected override IReadOnlyDictionary<string, Type> EventTypes { get; } =
        new Dictionary<string, Type>
        {
            [nameof(CourseRenamedEvent)] = typeof(CourseRenamedEvent)
        };

    protected override Task DispatchAsync(
        IServiceProvider services, IntegrationEvent @event, CancellationToken ct)
        => @event switch
        {
            CourseRenamedEvent renamed => services
                .GetRequiredService<CourseRenamedHandler>()
                .HandleAsync(renamed, ct),
            _ => throw new InvalidOperationException(
                $"No handler for {@event.GetType().Name}")
        };
}
