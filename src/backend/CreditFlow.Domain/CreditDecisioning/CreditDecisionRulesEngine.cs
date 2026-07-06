using CreditFlow.Domain.Applications.ValueObjects;
using CreditFlow.Domain.CreditDecisioning.Enums;
using CreditFlow.Domain.CreditDecisioning.ValueObjects;
using CreditFlow.Domain.Kyc.Enums;
using CreditFlow.Domain.Kyc.ValueObjects;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Domain.CreditDecisioning;

// Deterministic proposal-style credit rules.
// This approximates the older proposal-generation script using only data currently available
// in LoanApplication/BorrowerSnapshot: business age, monthly revenue, requested amount, currency,
// and KYC outcome. No external bureau/KYC/Serasa data is required here.
public static class CreditDecisionRulesEngine
{
    public const int OneYear = 12;
    public const int TwoYears = 12 * 2;
    public const int FiveYears = 12 * 5;
    public const int TenYears = 12 * 10;

    private const decimal MinimumProductAmount = 5_000m;
    private const decimal MaximumAutomaticRequestToAnnualRevenueRatio = 0.50m;

    public static CreditAssessment Assess(
        string assessmentId,
        string clientId,
        string applicationId,
        BorrowerSnapshot borrower,
        Money requestedAmount,
        Money monthlyRevenue,
        KycSnapshot kyc,
        DateTimeOffset now)
    {
        EnsureSameCurrency(requestedAmount, monthlyRevenue);

        if (kyc.Status == KycStatus.Failed)
        {
            return new CreditAssessment(
                assessmentId,
                clientId,
                applicationId,
                CreditAssessmentResult.Rejected,
                0,
                ["KYC failed. Credit assessment cannot continue."],
                [],
                now);
        }

        var annualRevenue = monthlyRevenue.Amount * 12m;
        var requestToAnnualRevenueRatio = requestedAmount.Amount / annualRevenue;

        if (requestToAnnualRevenueRatio > MaximumAutomaticRequestToAnnualRevenueRatio)
        {
            return new CreditAssessment(
                assessmentId,
                clientId,
                applicationId,
                CreditAssessmentResult.Rejected,
                20,
                ["Requested amount is too high relative to annual revenue."],
                [],
                now);
        }

        var proposalLimit = EstimateProposalLimit(
            borrower,
            requestedAmount,
            monthlyRevenue);

        if (proposalLimit.Amount < MinimumProductAmount)
        {
            return new CreditAssessment(
                assessmentId,
                clientId,
                applicationId,
                CreditAssessmentResult.Rejected,
                30,
                ["Estimated proposal limit is below the minimum product amount."],
                [],
                now);
        }

        var reasons = new List<string>();
        var score = 50;

        score += ScoreBusinessAge(borrower.MonthsInBusiness, reasons);
        score += ScoreRevenue(monthlyRevenue, reasons);
        score += ScoreAffordability(requestedAmount, monthlyRevenue, reasons);

        var requiresManualKycReview = kyc.Status == KycStatus.NeedsReview;
        var requestedAmountExceedsEstimatedLimit = requestedAmount > proposalLimit;

        if (requestedAmountExceedsEstimatedLimit)
        {
            score -= 15;
            reasons.Add("Requested amount exceeds the estimated automatic proposal limit; a lower-limit offer can be considered.");
        }

        if (requiresManualKycReview)
        {
            score -= 20;
            reasons.Add("KYC requires manual review.");

            // Unresolved KYC review must not produce automatic approval.
            score = Math.Min(score, 74);
        }

        if (requestedAmountExceedsEstimatedLimit)
        {
            // A smaller proposal may still be viable, but not as an automatic approval.
            score = Math.Min(score, 74);
        }

        score = Math.Clamp(score, 0, 100);

        var assessmentResult = ResolveAssessmentResult(
            score,
            requiresManualKycReview,
            requestedAmountExceedsEstimatedLimit);

        reasons.Add($"Estimated proposal limit is {proposalLimit.Amount:N2} {proposalLimit.Currency}.");
        reasons.Add($"Estimated base monthly rate is {EstimateBaseMonthlyRate(monthlyRevenue):N2}%.");

        var offers = BuildOffers(
            score,
            requestedAmount,
            proposalLimit,
            monthlyRevenue);

        return new CreditAssessment(
            assessmentId,
            clientId,
            applicationId,
            assessmentResult,
            score,
            reasons,
            offers,
            now);
    }

    private static CreditAssessmentResult ResolveAssessmentResult(
        int score,
        bool requiresManualKycReview,
        bool requestedAmountExceedsEstimatedLimit)
    {
        if (requiresManualKycReview)
        {
            return CreditAssessmentResult.ManualReview;
        }

        if (requestedAmountExceedsEstimatedLimit)
        {
            return CreditAssessmentResult.ManualReview;
        }

        return score switch
        {
            >= 75 => CreditAssessmentResult.Approved,
            >= 50 => CreditAssessmentResult.ManualReview,
            _ => CreditAssessmentResult.Rejected
        };
    }

    private static int ScoreBusinessAge(
        int monthsInBusiness,
        List<string> reasons)
    {
        if (monthsInBusiness < OneYear)
        {
            reasons.Add("Business operating history is below minimum preferred age.");
            return -30;
        }

        if (monthsInBusiness < TwoYears)
        {
            reasons.Add("Business operating history is short.");
            return -10;
        }

        if (monthsInBusiness < FiveYears)
        {
            reasons.Add("Business operating history is acceptable.");
            return 5;
        }

        if (monthsInBusiness < TenYears)
        {
            reasons.Add("Business operating history is strong.");
            return 10;
        }

        reasons.Add("Business operating history is very strong.");
        return 15;
    }

    private static int ScoreRevenue(
        Money monthlyRevenue,
        List<string> reasons)
    {
        if (monthlyRevenue.Amount <= 10_000m)
        {
            reasons.Add("Monthly revenue is near the minimum threshold.");
            return -15;
        }

        if (monthlyRevenue.Amount <= 20_000m)
        {
            reasons.Add("Monthly revenue is modest.");
            return -5;
        }

        if (monthlyRevenue.Amount <= 50_000m)
        {
            reasons.Add("Monthly revenue is acceptable.");
            return 5;
        }

        if (monthlyRevenue.Amount <= 100_000m)
        {
            reasons.Add("Monthly revenue is strong.");
            return 10;
        }

        reasons.Add("Monthly revenue is very strong.");
        return 15;
    }

    private static int ScoreAffordability(
        Money requestedAmount,
        Money monthlyRevenue,
        List<string> reasons)
    {
        var annualRevenue = monthlyRevenue.Amount * 12m;
        var ratio = requestedAmount.Amount / annualRevenue;

        if (ratio <= 0.10m)
        {
            reasons.Add("Requested amount is conservative relative to annual revenue.");
            return 20;
        }

        if (ratio <= 0.20m)
        {
            reasons.Add("Requested amount is reasonable relative to annual revenue.");
            return 10;
        }

        if (ratio <= 0.35m)
        {
            reasons.Add("Requested amount is elevated but still within reviewable range.");
            return 0;
        }

        reasons.Add("Requested amount is high relative to annual revenue.");
        return -20;
    }

    private static Money EstimateProposalLimit(
        BorrowerSnapshot borrower,
        Money requestedAmount,
        Money monthlyRevenue)
    {
        var annualRevenue = monthlyRevenue.Amount * 12m;
        var leverage = EstimateLeverage(borrower.MonthsInBusiness);
        var estimatedLimit = annualRevenue * leverage;
        var cappedLimit = Math.Min(estimatedLimit, requestedAmount.Amount);

        return new Money(
            Math.Round(cappedLimit, 2, MidpointRounding.AwayFromZero),
            requestedAmount.Currency);
    }

    private static decimal EstimateLeverage(int monthsInBusiness)
    {
        if (monthsInBusiness < TwoYears)
        {
            return 0.04m;
        }

        if (monthsInBusiness < FiveYears)
        {
            return 0.06m;
        }

        if (monthsInBusiness < TenYears)
        {
            return 0.08m;
        }

        return 0.10m;
    }

    private static decimal EstimateBaseMonthlyRate(Money monthlyRevenue)
    {
        if (monthlyRevenue.Amount <= 10_000m)
        {
            return 4.99m;
        }

        if (monthlyRevenue.Amount <= 50_000m)
        {
            return 3.99m;
        }

        if (monthlyRevenue.Amount <= 100_000m)
        {
            return 3.49m;
        }

        return 3.12m;
    }

    private static IReadOnlyCollection<CreditProductOffer> BuildOffers(
        int score,
        Money requestedAmount,
        Money proposalLimit,
        Money monthlyRevenue)
    {
        if (proposalLimit.Amount < MinimumProductAmount)
        {
            return [];
        }

        var baseRate = EstimateBaseMonthlyRate(monthlyRevenue);

        if (score >= 80)
        {
            return
            [
                CreditProductOffer.Create(
                    CreditProductCode.WorkingCapitalPrime,
                    "Working Capital Prime",
                    proposalLimit,
                    24,
                    Math.Max(1.00m, baseRate - 0.30m)),

                CreditProductOffer.Create(
                    CreditProductCode.ReceivablesAdvance,
                    "Receivables Advance",
                    proposalLimit * 0.8m,
                    12,
                    baseRate),

                CreditProductOffer.Create(
                    CreditProductCode.CreditLine,
                    "Business Credit Line",
                    proposalLimit * 0.6m,
                    18,
                    baseRate + 0.25m)
            ];
        }

        if (score >= 65)
        {
            return
            [
                CreditProductOffer.Create(
                    CreditProductCode.WorkingCapitalStandard,
                    "Working Capital Standard",
                    proposalLimit,
                    18,
                    baseRate + 0.25m),

                CreditProductOffer.Create(
                    CreditProductCode.ReceivablesAdvance,
                    "Receivables Advance",
                    proposalLimit * 0.6m,
                    12,
                    baseRate + 0.45m)
            ];
        }

        if (score >= 50)
        {
            return
            [
                CreditProductOffer.Create(
                    CreditProductCode.ManualReviewOfferCandidate,
                    "Manual Review Offer Candidate",
                    proposalLimit,
                    12,
                    baseRate + 0.80m)
            ];
        }

        return [];
    }

    private static void EnsureSameCurrency(
        Money requestedAmount,
        Money monthlyRevenue)
    {
        if (!string.Equals(
            requestedAmount.Currency,
            monthlyRevenue.Currency,
            StringComparison.Ordinal))
        {
            throw new DomainException(
                $"Currency mismatch: Cannot assess requested amount in {requestedAmount.Currency} with revenue in {monthlyRevenue.Currency}.");
        }
    }
}
