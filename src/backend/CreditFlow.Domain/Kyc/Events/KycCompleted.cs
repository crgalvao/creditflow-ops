using CreditFlow.Domain.Kyc.Enums;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Domain.Kyc.Events;

public sealed record KycCompleted(
    Guid EventId,
    string ClientId,
    string TaxId,
    KycStatus Status,
    DateTimeOffset OccurredAt) : IDomainEvent;
