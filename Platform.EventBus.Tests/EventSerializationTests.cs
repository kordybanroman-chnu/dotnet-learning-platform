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
    public void CreatedAndDeletedEvents_AreRecordsWithDistinctEventIds()
    {
        var created = new CourseCreatedEvent(102, "ASP.NET Core API", 1800m);
        var deleted = new CourseDeletedEvent(102, "ASP.NET Core API");

        Assert.NotEqual(created.EventId, deleted.EventId);
        Assert.NotEqual(Guid.Empty, created.EventId);
        Assert.Equal(created, created with { });
    }
}
