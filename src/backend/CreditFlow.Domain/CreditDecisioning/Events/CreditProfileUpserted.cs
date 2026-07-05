using CreditFlow.Domain.CreditDecisioning.Enums;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Domain.CreditDecisioning.Events;

public sealed record CreditProfileUpserted(
    Guid EventId,
    string ClientId,
    string LastAssessmentId,
    int CurrentScore,
    CreditAssessmentResult CurrentResult,
    DateTimeOffset OccurredAt) : IDomainEvent;
