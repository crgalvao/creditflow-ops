using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Domain.Kyc.Events;

public sealed record KycFailed(
    Guid EventId,
    string ClientId,
    string TaxId,
    IReadOnlyCollection<string> Reasons,
    DateTimeOffset OccurredAt) : IDomainEvent;
