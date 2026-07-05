using CreditFlow.Domain.CreditDecisioning.Enums;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Domain.CreditDecisioning.Events;

public sealed record CreditAssessmentCompleted(
    Guid EventId,
    string AssessmentId,
    string ClientId,
    string ApplicationId,
    CreditAssessmentResult Result,
    int Score,
    DateTimeOffset OccurredAt) : IDomainEvent;
