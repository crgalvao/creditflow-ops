using CreditFlow.Api.Extensions;
using CreditFlow.Api.Stores;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Api.Services;

public sealed partial class InMemoryDomainEventPublisher(
    ICreditFlowStore store,
    ILogger<InMemoryDomainEventPublisher> logger)
    : IDomainEventPublisher
{
    public async Task PublishAsync(
        string applicationId,
        IReadOnlyCollection<IDomainEvent> domainEvents,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (domainEvents.Count == 0)
        {
            return;
        }

        var records = domainEvents
            .Select(domainEvent => new DomainEventRecord(
                ApplicationId: applicationId,
                EventId: domainEvent.EventId,
                EventType: domainEvent.GetType().Name,
                EventVersion: 1,
                CorrelationId: correlationId,
                OccurredAt: domainEvent.OccurredAt,
                Source: "CreditFlow.Api",
                Data: domainEvent))
            .ToArray();

        await store.AppendEventsAsync(records, cancellationToken);

        foreach (var record in records)
        {
            logger.LogDomainEventPublished(
                record.EventType,
                record.ApplicationId,
                record.EventId,
                record.CorrelationId);
        }
    }
}
