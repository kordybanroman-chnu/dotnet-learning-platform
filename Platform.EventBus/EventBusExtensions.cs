using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Platform.EventBus;

public static class EventBusExtensions
{
    public static TBuilder AddEventBus<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.AddRabbitMQClient("messaging");

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ICorrelationAccessor, HttpCorrelationAccessor>();
        builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        builder.Services.AddHostedService<RabbitMqTopologyInitializer>();

        return builder;
    }
}

internal sealed class RabbitMqTopologyInitializer(
    IConnection connection,
    ILogger<RabbitMqTopologyInitializer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await RabbitMqTopology.DeclareAsync(channel, stoppingToken);
                logger.LogInformation(
                    "RabbitMQ topology declared: {Exchange}, {EnrollmentsQueue}, {ReviewsQueue}",
                    RabbitMqTopology.CatalogExchange,
                    RabbitMqTopology.EnrollmentsQueue,
                    RabbitMqTopology.ReviewsQueue);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not declare RabbitMQ topology; retrying in 5 s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
