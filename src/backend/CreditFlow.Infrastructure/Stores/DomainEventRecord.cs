namespace CreditFlow.Infrastructure.Stores;

public sealed record DomainEventRecord(
    string ApplicationId,
    Guid EventId,
    string EventType,
    int EventVersion,
    string CorrelationId,
    DateTimeOffset OccurredAt,
    string Source,
    object Data);
