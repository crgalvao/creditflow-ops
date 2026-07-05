using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Domain.Applications.Events;

public sealed record LoanApplicationSubmitted(
    Guid EventId,
    string ApplicationId,
    string OwnerUserId,
    string TaxId,
    DateTimeOffset OccurredAt) : IDomainEvent;
