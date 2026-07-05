using CreditFlow.Domain.CreditDecisioning.Enums;

namespace CreditFlow.Domain.CreditDecisioning.ValueObjects;

public sealed record CreditProfileSnapshot(
    string ClientId,
    int CurrentScore,
    CreditAssessmentResult CurrentResult,
    IReadOnlyCollection<CreditProductOffer> CurrentEligibleProducts,
    string LastAssessmentId,
    int Version,
    DateTimeOffset UpdatedAt);
