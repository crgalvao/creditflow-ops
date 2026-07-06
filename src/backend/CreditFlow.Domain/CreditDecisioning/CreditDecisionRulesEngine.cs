using CreditFlow.Domain.Applications.ValueObjects;
using CreditFlow.Domain.CreditDecisioning.Enums;
using CreditFlow.Domain.CreditDecisioning.ValueObjects;
using CreditFlow.Domain.Kyc.Enums;
using CreditFlow.Domain.Kyc.ValueObjects;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Domain.CreditDecisioning;

// Pure domain service. No IO, no persistence, no static mutable state.
public static class CreditDecisionRulesEngine
{
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

        var score = 50;
        var reasons = new List<string>();
        var requiresManualKycReview = kyc.Status == KycStatus.NeedsReview;

        if (borrower.MonthsInBusiness >= 24)
        {
            score += 20;
            reasons.Add("Business operating history is acceptable.");
        }
        else if (borrower.MonthsInBusiness < 12)
        {
            score -= 10;
            reasons.Add("Business operating history is short.");
        }

        if (monthlyRevenue >= requestedAmount * 0.5m)
        {
            score += 15;
            reasons.Add("Monthly revenue supports the requested amount.");
        }

        if (requestedAmount <= new Money(100_000m, requestedAmount.Currency))
        {
            score += 10;
            reasons.Add("Requested amount is within the preferred range.");
        }

        if (requestedAmount > monthlyRevenue * 3m)
        {
            score -= 20;
            reasons.Add("Requested amount is high relative to monthly revenue.");
        }

        if (requiresManualKycReview)
        {
            score -= 20;
            reasons.Add("KYC requires manual review.");

            // An unresolved KYC review must not produce an automatic approval.
            score = Math.Min(score, 74);
        }

        score = Math.Clamp(score, 0, 100);

        var assessmentResult = (requiresManualKycReview, score) switch
        {
            (true, _) => CreditAssessmentResult.ManualReview,
            (_, >= 75) => CreditAssessmentResult.Approved,
            (_, >= 50) => CreditAssessmentResult.ManualReview,
            _ => CreditAssessmentResult.Rejected
        };

        var offers = BuildOffers(score, requestedAmount);

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

    private static IReadOnlyCollection<CreditProductOffer> BuildOffers(
        int score,
        Money requestedAmount)
    {
        if (score >= 80)
        {
            return
            [
                CreditProductOffer.Create(
                    CreditProductCode.WorkingCapitalPrime,
                    "Working Capital Prime",
                    requestedAmount,
                    24,
                    1.49m),

                CreditProductOffer.Create(
                    CreditProductCode.ReceivablesAdvance,
                    "Receivables Advance",
                    requestedAmount * 0.8m,
                    12,
                    1.69m),

                CreditProductOffer.Create(
                    CreditProductCode.CreditLine,
                    "Business Credit Line",
                    requestedAmount * 0.6m,
                    18,
                    1.89m)
            ];
        }

        if (score >= 65)
        {
            return
            [
                CreditProductOffer.Create(
                    CreditProductCode.WorkingCapitalStandard,
                    "Working Capital Standard",
                    requestedAmount * 0.8m,
                    18,
                    2.19m),

                CreditProductOffer.Create(
                    CreditProductCode.ReceivablesAdvance,
                    "Receivables Advance",
                    requestedAmount * 0.6m,
                    12,
                    2.09m)
            ];
        }

        if (score >= 50)
        {
            return
            [
                CreditProductOffer.Create(
                    CreditProductCode.ManualReviewOfferCandidate,
                    "Manual Review Offer Candidate",
                    requestedAmount * 0.5m,
                    12,
                    2.79m)
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
