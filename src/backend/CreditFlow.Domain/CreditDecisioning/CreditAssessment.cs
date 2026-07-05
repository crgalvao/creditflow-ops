using CreditFlow.Domain.CreditDecisioning.Enums;
using CreditFlow.Domain.CreditDecisioning.Events;
using CreditFlow.Domain.CreditDecisioning.ValueObjects;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Domain.CreditDecisioning;

public sealed class CreditAssessment : AggregateRoot
{
    private readonly List<string> _reasons = [];

    private readonly List<CreditProductOffer> _eligibleProducts = [];

    public string AssessmentId { get; private set; } = string.Empty;

    public string ClientId { get; private set; } = string.Empty;

    public string ApplicationId { get; private set; } = string.Empty;

    public CreditAssessmentResult Result { get; private set; }

    public int Score { get; private set; }

    public DateTimeOffset AssessedAt { get; private set; }

    public IReadOnlyCollection<string> Reasons =>
        _reasons.AsReadOnly();

    public IReadOnlyCollection<CreditProductOffer> EligibleProducts =>
        _eligibleProducts.AsReadOnly();

    private CreditAssessment()
    {
    }

    internal CreditAssessment(
        string assessmentId,
        string clientId,
        string applicationId,
        CreditAssessmentResult result,
        int score,
        IEnumerable<string> reasons,
        IEnumerable<CreditProductOffer> offers,
        DateTimeOffset now)
    {
        AssessmentId = Guard.Required(assessmentId, nameof(assessmentId));
        ClientId = Guard.Required(clientId, nameof(clientId));
        ApplicationId = Guard.Required(applicationId, nameof(applicationId));
        Result = result;
        Score = Guard.InRange(score, nameof(score), 0, 100);
        AssessedAt = now;

        _reasons.AddRange(reasons);
        _eligibleProducts.AddRange(offers);

        AddDomainEvent(new CreditAssessmentCompleted(
            Guid.NewGuid(),
            AssessmentId,
            ClientId,
            ApplicationId,
            Result,
            Score,
            now));
    }

    public CreditAssessmentSnapshot ToSnapshot()
    {
        return new CreditAssessmentSnapshot(
            AssessmentId,
            ClientId,
            ApplicationId,
            Result,
            Score,
            _reasons.ToArray(),
            _eligibleProducts.ToArray(),
            AssessedAt);
    }
}
