using System.Text.Json;
using Platform.EventBus;
using Platform.EventBus.Events;

namespace Platform.EventBus.Tests;

public sealed class EventSerializationTests
{
    [Fact]
    public void PriceChangedEvent_RoundTripsWithMetadata()
    {
        var @event = new CoursePriceChangedEvent(101, "Основи C#", 1200m, 1350m)
        {
            CorrelationId = "corr-1"
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(@event, EventBusJson.Options);
        var restored = JsonSerializer.Deserialize<CoursePriceChangedEvent>(
            json, EventBusJson.Options);

        Assert.NotNull(restored);
        Assert.Equal(@event.EventId, restored.EventId);
        Assert.Equal(@event.OccurredAt, restored.OccurredAt);
        Assert.Equal("corr-1", restored.CorrelationId);
        Assert.Equal(101, restored.CourseId);
        Assert.Equal(1350m, restored.NewPrice);
    }

    [Fact]
    public void PriceChangedEvent_UsesCamelCaseNames()
    {
        var json = JsonSerializer.Serialize(
            new CoursePriceChangedEvent(101, "Основи C#", 1200m, 1350m),
            EventBusJson.Options);

        Assert.Contains("\"eventId\"", json);
        Assert.Contains("\"occurredAt\"", json);
        Assert.Contains("\"courseId\"", json);
        Assert.DoesNotContain("\"EventId\"", json);
    }

    [Fact]
    public void CreatedAndDeletedEvents_RoundTripWithDistinctEventIds()
    {
        var created = new CourseCreatedEvent(102, "ASP.NET Core API", 1800m)
        {
            CorrelationId = "corr-create"
        };
        var deleted = new CourseDeletedEvent(102, "ASP.NET Core API")
        {
            CorrelationId = "corr-delete"
        };

        var createdJson = JsonSerializer.SerializeToUtf8Bytes(created, EventBusJson.Options);
        var deletedJson = JsonSerializer.SerializeToUtf8Bytes(deleted, EventBusJson.Options);

        var restoredCreated = JsonSerializer.Deserialize<CourseCreatedEvent>(
            createdJson, EventBusJson.Options);
        var restoredDeleted = JsonSerializer.Deserialize<CourseDeletedEvent>(
            deletedJson, EventBusJson.Options);

        Assert.NotEqual(created.EventId, deleted.EventId);
        Assert.NotEqual(Guid.Empty, created.EventId);
        Assert.Equal(created.EventId, restoredCreated!.EventId);
        Assert.Equal(created.CourseId, restoredCreated.CourseId);
        Assert.Equal("corr-create", restoredCreated.CorrelationId);
        Assert.Equal(deleted.EventId, restoredDeleted!.EventId);
        Assert.Equal("corr-delete", restoredDeleted.CorrelationId);
    }
}
