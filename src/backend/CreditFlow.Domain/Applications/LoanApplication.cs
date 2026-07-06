using CreditFlow.Domain.Applications.Enums;
using CreditFlow.Domain.Applications.Events;
using CreditFlow.Domain.Applications.ValueObjects;
using CreditFlow.Domain.CreditDecisioning.Enums;
using CreditFlow.Domain.CreditDecisioning.ValueObjects;
using CreditFlow.Domain.Kyc.Enums;
using CreditFlow.Domain.Kyc.ValueObjects;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Domain.Applications;

public sealed class LoanApplication : AggregateRoot
{
    public string ApplicationId { get; private set; } = string.Empty;

    public string OwnerUserId { get; private set; } = string.Empty;

    public BorrowerSnapshot Borrower { get; private set; } = null!;

    public Money RequestedAmount { get; private set; }

    public Money MonthlyRevenue { get; private set; }

    public ApplicationStatus Status { get; private set; }

    public ApplicationKycState KycState { get; private set; }

    public ApplicationCreditState CreditState { get; private set; }

    public KycSnapshot? KycSnapshot { get; private set; }

    public CreditAssessmentSnapshot? CreditAssessmentSnapshot { get; private set; }

    public CreditProfileSnapshot? CreditProfileSnapshot { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private LoanApplication()
    {
    }

    private LoanApplication(
        string applicationId,
        string ownerUserId,
        BorrowerSnapshot borrower,
        Money requestedAmount,
        Money monthlyRevenue,
        DateTimeOffset now)
    {
        ApplicationId = Guard.Required(applicationId, nameof(applicationId), 80);
        OwnerUserId = Guard.Required(ownerUserId, nameof(ownerUserId), 120);
        Borrower = borrower;
        RequestedAmount = requestedAmount;
        MonthlyRevenue = monthlyRevenue;
        Status = ApplicationStatus.Submitted;
        KycState = ApplicationKycState.Pending;
        CreditState = ApplicationCreditState.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static LoanApplication Submit(
        string applicationId,
        string ownerUserId,
        BorrowerSnapshot borrower,
        Money requestedAmount,
        Money monthlyRevenue,
        DateTimeOffset now)
    {
        var application = new LoanApplication(
            applicationId,
            ownerUserId,
            borrower,
            requestedAmount,
            monthlyRevenue,
            now);

        application.AddDomainEvent(new LoanApplicationSubmitted(
            Guid.NewGuid(),
            application.ApplicationId,
            application.OwnerUserId,
            application.Borrower.TaxId,
            now));

        application.RequestKycCheck(now);

        return application;
    }

    public void RecordKycResult(
        KycSnapshot kycSnapshot,
        DateTimeOffset now)
    {
        if (Status != ApplicationStatus.KycInProgress)
        {
            throw new DomainException(
                "Cannot record KYC result unless KYC is in progress.");
        }

        if (!string.Equals(kycSnapshot.TaxId, Borrower.TaxId, StringComparison.Ordinal))
        {
            throw new DomainException(
                "Cannot record KYC result because the snapshot tax ID does not match the application borrower.");
        }

        KycSnapshot = kycSnapshot;
        UpdatedAt = now;

        KycState = kycSnapshot.Status switch
        {
            KycStatus.Verified => ApplicationKycState.Verified,
            KycStatus.NeedsReview => ApplicationKycState.NeedsReview,
            KycStatus.Failed => ApplicationKycState.Failed,
            _ => throw new DomainException("Cannot record unchecked KYC result.")
        };

        if (KycState == ApplicationKycState.Failed)
        {
            FailApplication(now);
            return;
        }

        RequestCreditAssessment(kycSnapshot.ClientId, now);
    }

    public void RecordCreditAssessment(
        CreditAssessmentSnapshot creditAssessment,
        DateTimeOffset now)
    {
        if (Status != ApplicationStatus.CreditInProgress)
        {
            throw new DomainException(
                "Cannot record credit assessment unless credit is in progress.");
        }

        if (!string.Equals(creditAssessment.ApplicationId, ApplicationId, StringComparison.Ordinal))
        {
            throw new DomainException(
                "Cannot record credit assessment because the snapshot application ID does not match this loan application.");
        }

        if (KycSnapshot is null)
        {
            throw new DomainException(
                "Cannot record credit assessment before recording the KYC snapshot.");
        }

        if (!string.Equals(creditAssessment.ClientId, KycSnapshot.ClientId, StringComparison.Ordinal))
        {
            throw new DomainException(
                "Cannot record credit assessment because the snapshot client ID does not match the KYC client.");
        }

        CreditAssessmentSnapshot = creditAssessment;
        CreditState = ToApplicationCreditState(creditAssessment.Result);
        UpdatedAt = now;
    }

    public void CompleteDecision(
        CreditProfileSnapshot creditProfile,
        DateTimeOffset now)
    {
        if (Status != ApplicationStatus.CreditInProgress)
        {
            throw new DomainException(
                "Cannot complete decision unless credit is in progress.");
        }

        if (CreditAssessmentSnapshot is null)
        {
            throw new DomainException("Cannot complete decision without a credit assessment snapshot.");
        }

        if (!string.Equals(creditProfile.ClientId, CreditAssessmentSnapshot.ClientId, StringComparison.Ordinal))
        {
            throw new DomainException(
                "Cannot complete decision because the credit profile client ID does not match the assessment client.");
        }

        if (!string.Equals(creditProfile.LastAssessmentId, CreditAssessmentSnapshot.AssessmentId, StringComparison.Ordinal))
        {
            throw new DomainException(
                "Cannot complete decision because the credit profile does not reference the recorded assessment.");
        }

        CreditProfileSnapshot = creditProfile;

        Status = CreditAssessmentSnapshot.Result switch
        {
            CreditAssessmentResult.Approved => ApplicationStatus.Approved,
            CreditAssessmentResult.ManualReview => ApplicationStatus.ManualReview,
            CreditAssessmentResult.Rejected => ApplicationStatus.Rejected,
            _ => throw new DomainException("Unsupported credit assessment result.")
        };

        UpdatedAt = now;

        AddDomainEvent(new LoanDecisionCompleted(
            Guid.NewGuid(),
            ApplicationId,
            Status,
            now));
    }

    private void RequestKycCheck(DateTimeOffset now)
    {
        Status = ApplicationStatus.KycInProgress;
        UpdatedAt = now;

        AddDomainEvent(new KycCheckRequested(
            Guid.NewGuid(),
            ApplicationId,
            Borrower.TaxId,
            now));
    }

    private void RequestCreditAssessment(
        string clientId,
        DateTimeOffset now)
    {
        Status = ApplicationStatus.CreditInProgress;
        UpdatedAt = now;

        AddDomainEvent(new CreditAssessmentRequested(
            Guid.NewGuid(),
            ApplicationId,
            clientId,
            Borrower.TaxId,
            RequestedAmount.Amount,
            now));
    }

    private void FailApplication(DateTimeOffset now)
    {
        Status = ApplicationStatus.Failed;
        UpdatedAt = now;

        AddDomainEvent(new LoanDecisionCompleted(
            Guid.NewGuid(),
            ApplicationId,
            Status,
            now));
    }

    private static ApplicationCreditState ToApplicationCreditState(
        CreditAssessmentResult result)
    {
        return result switch
        {
            CreditAssessmentResult.Approved => ApplicationCreditState.Approved,
            CreditAssessmentResult.ManualReview => ApplicationCreditState.ManualReview,
            CreditAssessmentResult.Rejected => ApplicationCreditState.Rejected,
            _ => throw new DomainException("Unsupported credit assessment result.")
        };
    }
}
