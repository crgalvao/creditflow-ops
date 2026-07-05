using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Domain.Applications.Events;

public sealed record KycCheckRequested(
    Guid EventId,
    string ApplicationId,
    string TaxId,
    DateTimeOffset OccurredAt) : IDomainEvent;
