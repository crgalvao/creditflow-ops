using CreditFlow.Domain.CreditDecisioning.Enums;

namespace CreditFlow.Domain.CreditDecisioning.ValueObjects;

public sealed record CreditAssessmentSnapshot(
    string AssessmentId,
    string ClientId,
    string ApplicationId,
    CreditAssessmentResult Result,
    int Score,
    IReadOnlyCollection<string> Reasons,
    IReadOnlyCollection<CreditProductOffer> EligibleProducts,
    DateTimeOffset AssessedAt);
