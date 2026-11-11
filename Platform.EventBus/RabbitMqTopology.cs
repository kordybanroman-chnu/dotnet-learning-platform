using RabbitMQ.Client;

namespace Platform.EventBus;

public static class RabbitMqTopology
{
    public const string CatalogExchange = "catalog-events";

    public const string EnrollmentsDeadLetterExchange = "enrollments.catalog-sync.dlx";
    public const string EnrollmentsQueue = "enrollments.catalog-sync";
    public const string EnrollmentsDeadLetterQueue = "enrollments.catalog-sync.dlq";
    public const string EnrollmentsBindingPattern = "course.*";

    public const string ReviewsDeadLetterExchange = "reviews.catalog-sync.dlx";
    public const string ReviewsQueue = "reviews.catalog-sync";
    public const string ReviewsDeadLetterQueue = "reviews.catalog-sync.dlq";
    public const string ReviewsBindingPattern = CourseRoutingKeys.CourseRenamed;

    public static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(
            CatalogExchange, ExchangeType.Topic, durable: true, autoDelete: false,
            arguments: null, cancellationToken: ct);

        await DeclareQueueAsync(
            channel, EnrollmentsQueue, EnrollmentsDeadLetterExchange,
            EnrollmentsDeadLetterQueue, EnrollmentsBindingPattern, ct);
        await DeclareQueueAsync(
            channel, ReviewsQueue, ReviewsDeadLetterExchange,
            ReviewsDeadLetterQueue, ReviewsBindingPattern, ct);
    }

    private static async Task DeclareQueueAsync(
        IChannel channel,
        string queue,
        string deadLetterExchange,
        string deadLetterQueue,
        string bindingPattern,
        CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(
            deadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false,
            arguments: null, cancellationToken: ct);

        await channel.QueueDeclareAsync(
            queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = deadLetterExchange
            },
            cancellationToken: ct);
        await channel.QueueBindAsync(queue, CatalogExchange, bindingPattern, cancellationToken: ct);

        await channel.QueueDeclareAsync(
            deadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: ct);
        await channel.QueueBindAsync(
            deadLetterQueue, deadLetterExchange, routingKey: "", cancellationToken: ct);
    }
}

public static class CourseRoutingKeys
{
    public const string CoursePriceChanged = "course.price_changed";
    public const string CourseRenamed = "course.renamed";
}
