using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.EventBus;

public abstract class RabbitMqEventConsumer(
    IConnection connection,
    IServiceScopeFactory scopes,
    ILogger logger) : BackgroundService
{
    private const int MaxRetries = 3;
    private IChannel? _channel;

    protected abstract string QueueName { get; }

    protected abstract IReadOnlyDictionary<string, Type> EventTypes { get; }

    protected abstract Task DispatchAsync(
        IServiceProvider services, IntegrationEvent @event, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                _channel = channel;

                await RabbitMqTopology.DeclareAsync(channel, stoppingToken);
                await channel.BasicQosAsync(0, prefetchCount: 10, global: false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += OnReceivedAsync;
                await channel.BasicConsumeAsync(
                    QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

                var channelClosed = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                channel.ChannelShutdownAsync += (_, _) =>
                {
                    channelClosed.TrySetResult();
                    return Task.CompletedTask;
                };

                logger.LogInformation("Consuming RabbitMQ queue {Queue}", QueueName);
                await Task.WhenAny(
                    channelClosed.Task, Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Consumer for {Queue} failed; reconnecting in 5 s", QueueName);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                }
            }
            finally
            {
                if (_channel is not null)
                {
                    await _channel.DisposeAsync();
                    _channel = null;
                }
            }
        }
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        using var activity = RabbitMqTrace.StartConsumerActivity(args, QueueName);
        var channel = _channel
            ?? throw new InvalidOperationException($"Consumer channel for {QueueName} is not open");
        var eventTypeName = args.BasicProperties.Type;
        IntegrationEvent? @event = null;

        try
        {
            if (string.IsNullOrWhiteSpace(eventTypeName)
                || !EventTypes.TryGetValue(eventTypeName, out var eventType))
                throw new JsonException($"Unsupported event type '{eventTypeName}'");

            @event = JsonSerializer.Deserialize(args.Body.Span, eventType, EventBusJson.Options)
                as IntegrationEvent
                ?? throw new JsonException("Message body is empty");

            await using (var scope = scopes.CreateAsyncScope())
            {
                await DispatchAsync(scope.ServiceProvider, @event, CancellationToken.None);
            }
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false);

            logger.LogInformation(
                "Acked {EventType} {EventId} from {Queue} CorrelationId {CorrelationId}",
                eventTypeName, @event.EventId, QueueName, @event.CorrelationId);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex,
                "Malformed {EventType} message {MessageId} in {Queue}; sending to DLQ",
                eventTypeName, args.BasicProperties.MessageId, QueueName);
            await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false);
        }
        catch (Exception ex)
        {
            await RetryOrDeadLetterAsync(channel, args, ex, eventTypeName, @event);
        }
    }

    private async Task RetryOrDeadLetterAsync(
        IChannel channel,
        BasicDeliverEventArgs args,
        Exception exception,
        string? eventTypeName,
        IntegrationEvent? @event)
    {
        var attempt = GetRetryCount(args);
        if (attempt >= MaxRetries)
        {
            logger.LogError(exception,
                "Message {MessageId} {EventType} {EventId} in {Queue} failed after {Attempts} attempts; sending to DLQ CorrelationId {CorrelationId}",
                args.BasicProperties.MessageId, eventTypeName, @event?.EventId, QueueName,
                attempt, @event?.CorrelationId);
            await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        try
        {
            var delay = TimeSpan.FromSeconds(1 << attempt);
            logger.LogWarning(exception,
                "Retry {Attempt}/{Max} for message {MessageId} {EventType} {EventId} in {Queue} after {DelaySeconds}s CorrelationId {CorrelationId}",
                attempt + 1, MaxRetries, args.BasicProperties.MessageId, eventTypeName,
                @event?.EventId, QueueName, delay.TotalSeconds, @event?.CorrelationId);
            await Task.Delay(delay, args.CancellationToken);

            var headers = args.BasicProperties.Headers is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(args.BasicProperties.Headers);
            headers["x-retry-count"] = attempt + 1;

            var properties = new BasicProperties(args.BasicProperties) { Headers = headers };
            await channel.BasicPublishAsync(
                args.Exchange, args.RoutingKey, false, properties, args.Body, args.CancellationToken);
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false);
        }
        catch (Exception publishException)
        {
            logger.LogError(publishException,
                "Could not republish message {MessageId} {EventType} {EventId}; requeueing original",
                args.BasicProperties.MessageId, eventTypeName, @event?.EventId);
            await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private static int GetRetryCount(BasicDeliverEventArgs args)
    {
        if (args.BasicProperties.Headers is null
            || !args.BasicProperties.Headers.TryGetValue("x-retry-count", out var value)
            || value is null)
            return 0;

        return value switch
        {
            int count => count,
            long count => (int)count,
            byte[] bytes => int.Parse(Encoding.UTF8.GetString(bytes)),
            string text => int.Parse(text),
            _ => Convert.ToInt32(value)
        };
    }
}
