using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.EventBus;

public static class RabbitMqTrace
{
    public const string SourceName = "Platform.Messaging";

    public static readonly ActivitySource Source = new(SourceName);

    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    public static void Inject(BasicProperties properties)
    {
        if (Activity.Current is not { } activity)
            return;

        properties.Headers ??= new Dictionary<string, object?>();
        Propagator.Inject(
            new PropagationContext(activity.Context, Baggage.Current),
            properties.Headers,
            (headers, key, value) => headers[key] = value);
    }

    public static Activity? StartConsumerActivity(BasicDeliverEventArgs args, string queue)
    {
        var parent = Propagator.Extract(
            default,
            args.BasicProperties.Headers,
            (headers, key) =>
                headers is not null && headers.TryGetValue(key, out var value) && value is byte[] bytes
                    ? [Encoding.UTF8.GetString(bytes)]
                    : []);

        var activity = Source.StartActivity(
            $"{queue} process", ActivityKind.Consumer, parent.ActivityContext);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", queue);
        activity?.SetTag("messaging.message.id", args.BasicProperties.MessageId);
        activity?.SetTag("messaging.message.type", args.BasicProperties.Type);
        activity?.SetTag("correlation.id", args.BasicProperties.CorrelationId);
        return activity;
    }
}
