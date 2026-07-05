using CreditFlow.Domain.Applications.Enums;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Domain.Applications.Events;

public sealed record LoanDecisionCompleted(
    Guid EventId,
    string ApplicationId,
    ApplicationStatus FinalStatus,
    DateTimeOffset OccurredAt) : IDomainEvent;
