using CreditFlow.Domain.Applications.ValueObjects;
using CreditFlow.Domain.Kyc.Enums;
using CreditFlow.Domain.Kyc.Events;
using CreditFlow.Domain.Kyc.ValueObjects;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Domain.Kyc;

public sealed class ClientProfile : AggregateRoot
{
    private readonly List<string> _riskFlags = [];

    public string ClientId { get; private set; } = string.Empty;

    public string TaxId { get; private set; } = string.Empty;

    public string LegalName { get; private set; } = string.Empty;

    public string Industry { get; private set; } = string.Empty;

    public ClientProfileStatus Status { get; private set; }

    public KycStatus KycStatus { get; private set; }

    public int Version { get; private set; }

    public IReadOnlyCollection<string> RiskFlags =>
        _riskFlags.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private ClientProfile()
    {
    }

    private ClientProfile(
        string clientId,
        string taxId,
        string legalName,
        string industry,
        DateTimeOffset now)
    {
        ClientId = Guard.Required(clientId, nameof(clientId), 120);
        TaxId = Guard.Required(taxId, nameof(taxId), 80);
        LegalName = Guard.Required(legalName, nameof(legalName), 160);
        Industry = Guard.Required(industry, nameof(industry), 80);
        Status = ClientProfileStatus.Active;
        KycStatus = KycStatus.NotChecked;
        Version = 1;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static ClientProfile Create(
        string clientId,
        string taxId,
        string legalName,
        string industry,
        DateTimeOffset now)
    {
        return new ClientProfile(
            clientId,
            taxId,
            legalName,
            industry,
            now);
    }

    public void UpsertFromBorrowerSnapshot(
        BorrowerSnapshot borrower,
        DateTimeOffset now)
    {
        TaxId = Guard.Required(borrower.TaxId, nameof(borrower.TaxId), 80);
        LegalName = Guard.Required(borrower.LegalName, nameof(borrower.LegalName), 160);
        Industry = Guard.Required(borrower.Industry, nameof(borrower.Industry), 80);
        Version++;
        UpdatedAt = now;
    }

    public KycSnapshot EvaluateKyc(
        bool matchesSanctionList,
        bool missingDocs,
        DateTimeOffset now)
    {
        _riskFlags.Clear();

        if (matchesSanctionList)
        {
            KycStatus = KycStatus.Failed;
            Status = ClientProfileStatus.Blocked;
            _riskFlags.Add("Matched blocked name rule.");
        }
        else if (missingDocs)
        {
            KycStatus = KycStatus.NeedsReview;
            Status = ClientProfileStatus.NeedsReview;
            _riskFlags.Add("Missing required documentation.");
        }
        else
        {
            KycStatus = KycStatus.Verified;
            Status = ClientProfileStatus.Active;
        }

        Version++;
        UpdatedAt = now;

        var snapshot = ToSnapshot(now);

        AddKycOutcomeEvent(snapshot, now);

        return snapshot;
    }

    public KycSnapshot ToSnapshot(DateTimeOffset checkedAt)
    {
        return new KycSnapshot(
            ClientId,
            TaxId,
            LegalName,
            KycStatus,
            _riskFlags.ToArray(),
            Version,
            checkedAt);
    }

    private void AddKycOutcomeEvent(KycSnapshot snapshot, DateTimeOffset now)
    {
        switch (snapshot.Status)
        {
            case KycStatus.Verified:
                AddDomainEvent(new KycCompleted(
                    Guid.NewGuid(),
                    ClientId,
                    TaxId,
                    snapshot.Status,
                    now));
                break;

            case KycStatus.NeedsReview:
                AddDomainEvent(new KycNeedsReview(
                    Guid.NewGuid(),
                    ClientId,
                    TaxId,
                    snapshot.RiskFlags,
                    now));
                break;

            case KycStatus.Failed:
                AddDomainEvent(new KycFailed(
                    Guid.NewGuid(),
                    ClientId,
                    TaxId,
                    snapshot.RiskFlags,
                    now));
                break;

            case KycStatus.NotChecked:
            default:
                throw new DomainException("Cannot emit KYC outcome for an unchecked profile.");
        }
    }
}
