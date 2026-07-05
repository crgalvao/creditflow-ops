using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Domain.Applications.Events;

public sealed record CreditAssessmentRequested(
    Guid EventId,
    string ApplicationId,
    string ClientId,
    string TaxId,
    decimal RequestedAmount,
    DateTimeOffset OccurredAt) : IDomainEvent;
