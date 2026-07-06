using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Applications.Enums;
using CreditFlow.Domain.Applications.Events;
using CreditFlow.Domain.Applications.ValueObjects;
using CreditFlow.Domain.CreditDecisioning;
using CreditFlow.Domain.CreditDecisioning.Enums;
using CreditFlow.Domain.Kyc.Enums;
using CreditFlow.Domain.Kyc.ValueObjects;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.UnitTests.Domain;

public sealed class LoanApplicationWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Submit_CreatesApplicationInKycInProgressAndRaisesInitialEvents()
    {
        var application = SubmitApplication();

        Assert.Equal(ApplicationStatus.KycInProgress, application.Status);
        Assert.Equal(ApplicationKycState.Pending, application.KycState);
        Assert.Equal(ApplicationCreditState.Pending, application.CreditState);

        Assert.Contains(application.DomainEvents, domainEvent => domainEvent is LoanApplicationSubmitted);
        Assert.Contains(application.DomainEvents, domainEvent => domainEvent is KycCheckRequested);
    }

    [Fact]
    public void RecordKycResult_WhenVerified_MovesApplicationToCreditInProgress()
    {
        var application = SubmitApplication();
        application.ClearDomainEvents();

        application.RecordKycResult(Kyc(KycStatus.Verified), Now.AddMinutes(1));

        Assert.Equal(ApplicationStatus.CreditInProgress, application.Status);
        Assert.Equal(ApplicationKycState.Verified, application.KycState);
        Assert.NotNull(application.KycSnapshot);
        Assert.Contains(application.DomainEvents, domainEvent => domainEvent is CreditAssessmentRequested);
    }

    [Fact]
    public void RecordKycResult_WhenNeedsReview_MovesApplicationToCreditInProgressWithKycReviewState()
    {
        var application = SubmitApplication();

        application.RecordKycResult(Kyc(KycStatus.NeedsReview), Now.AddMinutes(1));

        Assert.Equal(ApplicationStatus.CreditInProgress, application.Status);
        Assert.Equal(ApplicationKycState.NeedsReview, application.KycState);
        Assert.NotNull(application.KycSnapshot);
    }

    [Fact]
    public void RecordKycResult_WhenFailed_FailsApplication()
    {
        var application = SubmitApplication();

        application.RecordKycResult(Kyc(KycStatus.Failed), Now.AddMinutes(1));

        Assert.Equal(ApplicationStatus.Failed, application.Status);
        Assert.Equal(ApplicationKycState.Failed, application.KycState);
        Assert.Contains(application.DomainEvents, domainEvent => domainEvent is LoanDecisionCompleted);
    }

    [Fact]
    public void CompleteDecision_WhenAssessmentIsApproved_CompletesApplicationAsApproved()
    {
        var application = SubmitApplication();
        application.RecordKycResult(Kyc(KycStatus.Verified), Now.AddMinutes(1));

        var assessment = CreditDecisionRulesEngine.Assess(
            assessmentId: "assess-approved-001",
            clientId: "client-001",
            applicationId: application.ApplicationId,
            borrower: application.Borrower,
            requestedAmount: application.RequestedAmount,
            monthlyRevenue: application.MonthlyRevenue,
            kyc: application.KycSnapshot!,
            now: Now.AddMinutes(2));

        application.RecordCreditAssessment(assessment.ToSnapshot(), Now.AddMinutes(3));

        var creditProfile = CreditProfile.Create(assessment.ClientId);
        var creditProfileSnapshot = creditProfile.UpsertFromAssessment(assessment, Now.AddMinutes(4));

        application.CompleteDecision(creditProfileSnapshot, Now.AddMinutes(5));

        Assert.Equal(ApplicationStatus.Approved, application.Status);
        Assert.Equal(ApplicationCreditState.Approved, application.CreditState);
        Assert.NotNull(application.CreditProfileSnapshot);
        Assert.Contains(application.DomainEvents, domainEvent => domainEvent is LoanDecisionCompleted);
    }

    [Fact]
    public void RecordCreditAssessment_WhenAssessmentApplicationIdDoesNotMatch_ThrowsDomainException()
    {
        var application = SubmitApplication();
        application.RecordKycResult(Kyc(KycStatus.Verified), Now.AddMinutes(1));

        var wrongAssessment = CreditDecisionRulesEngine.Assess(
            assessmentId: "assess-wrong-001",
            clientId: "client-001",
            applicationId: "wrong-application-id",
            borrower: application.Borrower,
            requestedAmount: application.RequestedAmount,
            monthlyRevenue: application.MonthlyRevenue,
            kyc: application.KycSnapshot!,
            now: Now.AddMinutes(2));

        var exception = Assert.Throws<DomainException>(() =>
            application.RecordCreditAssessment(wrongAssessment.ToSnapshot(), Now.AddMinutes(3)));

        Assert.Contains("application ID does not match", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompleteDecision_WhenCreditProfileDoesNotReferenceAssessment_ThrowsDomainException()
    {
        var application = SubmitApplication();
        application.RecordKycResult(Kyc(KycStatus.Verified), Now.AddMinutes(1));

        var assessment = CreditDecisionRulesEngine.Assess(
            assessmentId: "assess-approved-001",
            clientId: "client-001",
            applicationId: application.ApplicationId,
            borrower: application.Borrower,
            requestedAmount: application.RequestedAmount,
            monthlyRevenue: application.MonthlyRevenue,
            kyc: application.KycSnapshot!,
            now: Now.AddMinutes(2));

        application.RecordCreditAssessment(assessment.ToSnapshot(), Now.AddMinutes(3));

        var mismatchedProfileSnapshot = new CreditFlow.Domain.CreditDecisioning.ValueObjects.CreditProfileSnapshot(
            ClientId: assessment.ClientId,
            CurrentScore: assessment.Score,
            CurrentResult: CreditAssessmentResult.Approved,
            CurrentEligibleProducts: assessment.EligibleProducts,
            LastAssessmentId: "different-assessment-id",
            Version: 1,
            UpdatedAt: Now.AddMinutes(4));

        var exception = Assert.Throws<DomainException>(() =>
            application.CompleteDecision(mismatchedProfileSnapshot, Now.AddMinutes(5)));

        Assert.Contains("does not reference the recorded assessment", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static LoanApplication SubmitApplication()
    {
        return LoanApplication.Submit(
            applicationId: "app-001",
            ownerUserId: "user-001",
            borrower: BorrowerSnapshot.Create(
                legalName: "Demo Coffee Imports Ltd",
                taxId: "DEMO-TAX-001",
                industry: "Wholesale",
                monthsInBusiness: 84),
            requestedAmount: new Money(50_000m, "USD"),
            monthlyRevenue: new Money(90_000m, "USD"),
            now: Now);
    }

    private static KycSnapshot Kyc(KycStatus status)
    {
        return new KycSnapshot(
            ClientId: "client-001",
            TaxId: "DEMO-TAX-001",
            LegalName: "Demo Coffee Imports Ltd",
            Status: status,
            RiskFlags: [],
            Version: 1,
            CheckedAt: Now);
    }
}
