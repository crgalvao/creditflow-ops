using CreditFlow.Domain.CreditDecisioning.Enums;
using CreditFlow.Domain.CreditDecisioning.Events;
using CreditFlow.Domain.CreditDecisioning.ValueObjects;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Domain.CreditDecisioning;

public sealed class CreditProfile : AggregateRoot
{
    private readonly List<CreditProductOffer> _currentEligibleProducts = [];

    public string ClientId { get; private set; } = string.Empty;

    public int CurrentScore { get; private set; }

    public CreditAssessmentResult CurrentResult { get; private set; }

    public string LastAssessmentId { get; private set; } = string.Empty;

    public int Version { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<CreditProductOffer> CurrentEligibleProducts =>
        _currentEligibleProducts.AsReadOnly();

    private CreditProfile()
    {
    }

    private CreditProfile(string clientId)
    {
        ClientId = Guard.Required(clientId, nameof(clientId));
        Version = 0;
    }

    public static CreditProfile Create(string clientId)
    {
        return new CreditProfile(clientId);
    }

    public CreditProfileSnapshot UpsertFromAssessment(
        CreditAssessment assessment,
        DateTimeOffset now)
    {
        if (!string.Equals(ClientId, assessment.ClientId, StringComparison.Ordinal))
        {
            throw new DomainException("Credit assessment client does not match credit profile client.");
        }

        CurrentScore = assessment.Score;
        CurrentResult = assessment.Result;
        LastAssessmentId = assessment.AssessmentId;
        UpdatedAt = now;
        Version++;

        _currentEligibleProducts.Clear();
        _currentEligibleProducts.AddRange(assessment.EligibleProducts);

        var snapshot = ToSnapshot();

        AddDomainEvent(new CreditProfileUpserted(
            Guid.NewGuid(),
            ClientId,
            LastAssessmentId,
            CurrentScore,
            CurrentResult,
            now));

        return snapshot;
    }

    public CreditProfileSnapshot ToSnapshot()
    {
        return new CreditProfileSnapshot(
            ClientId,
            CurrentScore,
            CurrentResult,
            _currentEligibleProducts.ToArray(),
            LastAssessmentId,
            Version,
            UpdatedAt);
    }
}
