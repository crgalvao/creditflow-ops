using CreditFlow.Domain.Applications.ValueObjects;
using CreditFlow.Domain.CreditDecisioning;
using CreditFlow.Domain.CreditDecisioning.Enums;
using CreditFlow.Domain.Kyc.Enums;
using CreditFlow.Domain.Kyc.ValueObjects;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.UnitTests.Domain;

public sealed class CreditDecisionRulesEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Assess_WhenBorrowerIsStrongAndWithinProposalLimit_ReturnsApproved()
    {
        var assessment = CreditDecisionRulesEngine.Assess(
            assessmentId: "assess-approved-001",
            clientId: "client-001",
            applicationId: "app-001",
            borrower: Borrower(monthsInBusiness: 84),
            requestedAmount: Usd(50_000m),
            monthlyRevenue: Usd(90_000m),
            kyc: Kyc(KycStatus.Verified),
            now: Now);

        Assert.Equal(CreditAssessmentResult.Approved, assessment.Result);
        Assert.True(assessment.Score >= 75);
        Assert.NotEmpty(assessment.EligibleProducts);
        Assert.Contains(assessment.Reasons, reason => reason.Contains("proposal limit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Assess_WhenRequestedAmountExceedsEstimatedProposalLimit_ReturnsManualReview()
    {
        var assessment = CreditDecisionRulesEngine.Assess(
            assessmentId: "assess-review-001",
            clientId: "client-001",
            applicationId: "app-001",
            borrower: Borrower(monthsInBusiness: 36),
            requestedAmount: Usd(120_000m),
            monthlyRevenue: Usd(60_000m),
            kyc: Kyc(KycStatus.Verified),
            now: Now);

        Assert.Equal(CreditAssessmentResult.ManualReview, assessment.Result);
        Assert.True(assessment.Score < 75);
        Assert.NotEmpty(assessment.EligibleProducts);
        Assert.Contains(
            assessment.Reasons,
            reason => reason.Contains("lower-limit offer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Assess_WhenRequestedAmountIsTooHighRelativeToAnnualRevenue_ReturnsRejected()
    {
        var assessment = CreditDecisionRulesEngine.Assess(
            assessmentId: "assess-rejected-001",
            clientId: "client-001",
            applicationId: "app-001",
            borrower: Borrower(monthsInBusiness: 84),
            requestedAmount: Usd(400_000m),
            monthlyRevenue: Usd(50_000m),
            kyc: Kyc(KycStatus.Verified),
            now: Now);

        Assert.Equal(CreditAssessmentResult.Rejected, assessment.Result);
        Assert.Empty(assessment.EligibleProducts);
        Assert.Contains(
            assessment.Reasons,
            reason => reason.Contains("too high relative to annual revenue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Assess_WhenKycNeedsReview_DoesNotAutoApprove()
    {
        var assessment = CreditDecisionRulesEngine.Assess(
            assessmentId: "assess-kyc-review-001",
            clientId: "client-001",
            applicationId: "app-001",
            borrower: Borrower(monthsInBusiness: 120),
            requestedAmount: Usd(40_000m),
            monthlyRevenue: Usd(150_000m),
            kyc: Kyc(KycStatus.NeedsReview),
            now: Now);

        Assert.Equal(CreditAssessmentResult.ManualReview, assessment.Result);
        Assert.True(assessment.Score < 75);
        Assert.Contains(
            assessment.Reasons,
            reason => reason.Contains("KYC requires manual review", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Assess_WhenKycFailed_ReturnsRejected()
    {
        var assessment = CreditDecisionRulesEngine.Assess(
            assessmentId: "assess-kyc-failed-001",
            clientId: "client-001",
            applicationId: "app-001",
            borrower: Borrower(monthsInBusiness: 120),
            requestedAmount: Usd(40_000m),
            monthlyRevenue: Usd(150_000m),
            kyc: Kyc(KycStatus.Failed),
            now: Now);

        Assert.Equal(CreditAssessmentResult.Rejected, assessment.Result);
        Assert.Equal(0, assessment.Score);
        Assert.Empty(assessment.EligibleProducts);
    }

    [Fact]
    public void Assess_WhenProposalLimitIsBelowMinimumProductAmount_ReturnsRejected()
    {
        var assessment = CreditDecisionRulesEngine.Assess(
            assessmentId: "assess-low-limit-001",
            clientId: "client-001",
            applicationId: "app-001",
            borrower: Borrower(monthsInBusiness: 12),
            requestedAmount: Usd(10_000m),
            monthlyRevenue: Usd(5_000m),
            kyc: Kyc(KycStatus.Verified),
            now: Now);

        Assert.Equal(CreditAssessmentResult.Rejected, assessment.Result);
        Assert.Equal(30, assessment.Score);
        Assert.Empty(assessment.EligibleProducts);
        Assert.Contains(
            assessment.Reasons,
            reason => reason.Contains("below the minimum product amount", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Assess_WhenCurrenciesDoNotMatch_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() =>
            CreditDecisionRulesEngine.Assess(
                assessmentId: "assess-currency-001",
                clientId: "client-001",
                applicationId: "app-001",
                borrower: Borrower(monthsInBusiness: 84),
                requestedAmount: new Money(50_000m, "USD"),
                monthlyRevenue: new Money(90_000m, "BRL"),
                kyc: Kyc(KycStatus.Verified),
                now: Now));

        Assert.Contains("Currency mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static BorrowerSnapshot Borrower(int monthsInBusiness)
    {
        return BorrowerSnapshot.Create(
            legalName: "Demo Borrower Ltd",
            taxId: "DEMO-TAX-001",
            industry: "Wholesale",
            monthsInBusiness: monthsInBusiness);
    }

    private static KycSnapshot Kyc(KycStatus status)
    {
        return new KycSnapshot(
            ClientId: "client-001",
            TaxId: "DEMO-TAX-001",
            LegalName: "Demo Borrower Ltd",
            Status: status,
            RiskFlags: [],
            Version: 1,
            CheckedAt: Now);
    }

    private static Money Usd(decimal amount) => new(amount, "USD");
}
