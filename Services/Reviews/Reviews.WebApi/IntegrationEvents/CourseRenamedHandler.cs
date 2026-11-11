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
        var processed = context.Database.GetCollection<BsonDocument>(ProcessedEventsCollection);
        var eventId = @event.EventId.ToString();
        var eventFilter = Builders<BsonDocument>.Filter.Eq("_id", eventId);

        if (await processed.CountDocumentsAsync(eventFilter, cancellationToken: ct) > 0)
        {
            logger.LogInformation(
                "Duplicate {EventType} {EventId} for course {CourseId} skipped",
                nameof(CourseRenamedEvent), @event.EventId, @event.CourseId);
            return;
        }

        var result = await context.Reviews.UpdateManyAsync(
            Builders<Review>.Filter.Eq(r => r.CourseId, (int)@event.CourseId),
            Builders<Review>.Update.Set(r => r.CourseTitle, @event.NewTitle),
            cancellationToken: ct);

        try
        {
            await processed.InsertOneAsync(
                new BsonDocument
                {
                    ["_id"] = eventId,
                    ["eventType"] = nameof(CourseRenamedEvent),
                    ["processedAt"] = @event.OccurredAt
                },
                cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            logger.LogInformation(
                "Duplicate {EventType} {EventId} for course {CourseId} was recorded concurrently",
                nameof(CourseRenamedEvent), @event.EventId, @event.CourseId);
        }

        logger.LogInformation(
            "Applied {EventType} {EventId} to course {CourseId}: title {OldTitle} -> {NewTitle}, reviews updated {MatchedCount}",
            nameof(CourseRenamedEvent), @event.EventId, @event.CourseId,
            @event.OldTitle, @event.NewTitle, result.MatchedCount);
    }
}
