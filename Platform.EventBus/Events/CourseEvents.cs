namespace Platform.EventBus.Events;

public sealed record CoursePriceChangedEvent(
    long CourseId,
    string Title,
    decimal OldPrice,
    decimal NewPrice) : IntegrationEvent;

public sealed record CourseRenamedEvent(
    long CourseId,
    string OldTitle,
    string NewTitle) : IntegrationEvent;

public sealed record CourseCreatedEvent(
    long CourseId,
    string Title,
    decimal Price) : IntegrationEvent;

public sealed record CourseDeletedEvent(
    long CourseId,
    string Title) : IntegrationEvent;
