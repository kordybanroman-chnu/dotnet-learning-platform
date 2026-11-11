using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Platform.EventBus.Events;
using Reviews.Domain.Entities;
using Reviews.Infrastructure.Persistence;

namespace Reviews.WebApi.IntegrationEvents;

public sealed class CourseRenamedHandler(
    MongoDbContext context,
    ILogger<CourseRenamedHandler> logger)
{
    public const string ProcessedEventsCollection = "processedEvents";

    public async Task HandleAsync(CourseRenamedEvent @event, CancellationToken ct)
    {
        if (!context.SupportsTransactions)
        {
            await ApplyAsync(@event, null, ct);
            return;
        }

        using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();
        try
        {
            await ApplyAsync(@event, session, ct);
            await session.CommitTransactionAsync(ct);
        }
        catch
        {
            await session.AbortTransactionAsync(ct);
            throw;
        }
    }

    private async Task ApplyAsync(
        CourseRenamedEvent @event, IClientSessionHandle? session, CancellationToken ct)
    {
        var processed = context.Database.GetCollection<BsonDocument>(ProcessedEventsCollection);
        var eventId = @event.EventId.ToString();
        var eventFilter = Builders<BsonDocument>.Filter.Eq("_id", eventId);

        var alreadyProcessed = session is null
            ? await processed.CountDocumentsAsync(eventFilter, cancellationToken: ct) > 0
            : await processed.CountDocumentsAsync(session, eventFilter, cancellationToken: ct) > 0;

        if (alreadyProcessed)
        {
            logger.LogInformation(
                "Duplicate {EventType} {EventId} for course {CourseId} skipped",
                nameof(CourseRenamedEvent), @event.EventId, @event.CourseId);
            return;
        }

        var filter = Builders<Review>.Filter.Eq(r => r.CourseId, (int)@event.CourseId)
                     & Builders<Review>.Filter.Or(
                         Builders<Review>.Filter.Eq(r => r.CourseTitleUpdatedAt, (DateTime?)null),
                         Builders<Review>.Filter.Lte(
                             r => r.CourseTitleUpdatedAt, @event.OccurredAt));

        var update = Builders<Review>.Update
            .Set(r => r.CourseTitle, @event.NewTitle)
            .Set(r => r.CourseTitleUpdatedAt, (DateTime?)@event.OccurredAt);

        var result = session is null
            ? await context.Reviews.UpdateManyAsync(filter, update, cancellationToken: ct)
            : await context.Reviews.UpdateManyAsync(session, filter, update, cancellationToken: ct);

        var document = new BsonDocument
        {
            ["_id"] = eventId,
            ["eventId"] = eventId,
            ["eventType"] = nameof(CourseRenamedEvent),
            ["processedAt"] = @event.OccurredAt
        };

        if (session is null)
            await processed.InsertOneAsync(document, cancellationToken: ct);
        else
            await processed.InsertOneAsync(session, document, cancellationToken: ct);

        logger.LogInformation(
            "Applied {EventType} {EventId} to course {CourseId}: title {OldTitle} -> {NewTitle}, reviews updated {MatchedCount}, CorrelationId {CorrelationId}",
            nameof(CourseRenamedEvent), @event.EventId, @event.CourseId,
            @event.OldTitle, @event.NewTitle, result.MatchedCount, @event.CorrelationId);
    }
}
