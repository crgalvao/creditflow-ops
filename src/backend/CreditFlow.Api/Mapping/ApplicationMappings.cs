using CreditFlow.Api.Contracts.Applications;
using CreditFlow.Api.Contracts.CreditDecisioning;
using CreditFlow.Api.Contracts.Kyc;
using CreditFlow.Infrastructure.Stores;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Applications.Enums;
using CreditFlow.Domain.CreditDecisioning;
using CreditFlow.Domain.CreditDecisioning.ValueObjects;
using CreditFlow.Domain.Kyc.ValueObjects;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Api.Mapping;

public static class ApplicationMappings
{
    public static SubmitLoanApplicationResponse ToSubmitResponse(
        this LoanApplication application) =>
        new(
            application.ApplicationId,
            application.Status,
            application.KycState,
            application.CreditState,
            "KycCheckRequested",
            application.CreatedAt);

    public static LoanApplicationSummaryResponse ToSummaryResponse(
        this LoanApplication application) =>
        new(
            application.ApplicationId,
            application.OwnerUserId,
            application.Borrower.LegalName,
            application.Borrower.TaxId,
            application.RequestedAmount.Amount,
            application.RequestedAmount.Currency,
            application.Status,
            application.KycState,
            application.CreditState,
            application.CreatedAt,
            application.UpdatedAt);

    public static LoanApplicationDetailsResponse ToDetailsResponse(
        this LoanApplication application) =>
        new(
            application.ApplicationId,
            application.OwnerUserId,
            new BorrowerResponse(
                application.Borrower.LegalName,
                application.Borrower.TaxId,
                application.Borrower.Industry,
                application.Borrower.MonthsInBusiness),
            application.RequestedAmount.ToResponse(),
            application.MonthlyRevenue.ToResponse(),
            application.Status,
            application.KycState,
            application.CreditState,
            application.KycSnapshot?.ToResponse(),
            application.CreditAssessmentSnapshot?.ToResponse(),
            application.CreditProfileSnapshot?.ToResponse(),
            application.CreatedAt,
            application.UpdatedAt);

    public static LoanDecisionResponse ToDecisionResponse(
        this LoanApplication application) =>
        new(
            application.ApplicationId,
            application.Status,
            application.KycState,
            application.CreditState,
            application.KycSnapshot?.ToResponse(),
            application.CreditAssessmentSnapshot?.ToResponse(),
            application.CreditProfileSnapshot?.ToResponse(),
            application.UpdatedAt);

    public static KycResultResponse ToKycResultResponse(
        this LoanApplication application,
        KycSnapshot kycSnapshot) =>
        new(
            application.ApplicationId,
            application.Status,
            application.KycState,
            kycSnapshot.Status,
            application.Status == ApplicationStatus.Failed
                ? "LoanDecisionCompleted"
                : "CreditAssessmentRequested",
            kycSnapshot.ToResponse());

    public static CreditAssessmentResultResponse ToCreditAssessmentResultResponse(
        this LoanApplication application,
        CreditAssessment assessment) =>
        new(
            application.ApplicationId,
            application.Status,
            application.CreditState,
            assessment.Result,
            assessment.Score,
            "CreditProfileUpserted",
            assessment.ToSnapshot().ToResponse());

    public static CreditProfileUpsertResultResponse ToCreditProfileUpsertResultResponse(
        this LoanApplication application,
        CreditProfileSnapshot creditProfile) =>
        new(
            application.ApplicationId,
            application.Status,
            creditProfile.ToResponse(),
            application.ToDecisionResponse());

    public static DomainEventResponse ToResponse(
        this DomainEventRecord domainEvent) =>
        new(
            domainEvent.EventId,
            domainEvent.EventType,
            domainEvent.EventVersion,
            domainEvent.CorrelationId,
            domainEvent.OccurredAt,
            domainEvent.Source,
            domainEvent.Data);

    private static MoneyResponse ToResponse(
        this Money money) =>
        new(money.Amount, money.Currency);

    private static KycSnapshotResponse ToResponse(
        this KycSnapshot snapshot) =>
        new(
            snapshot.ClientId,
            snapshot.TaxId,
            snapshot.LegalName,
            snapshot.Status,
            snapshot.RiskFlags,
            snapshot.Version,
            snapshot.CheckedAt);

    private static CreditAssessmentSnapshotResponse ToResponse(
        this CreditAssessmentSnapshot snapshot) =>
        new(
            snapshot.AssessmentId,
            snapshot.ClientId,
            snapshot.ApplicationId,
            snapshot.Result,
            snapshot.Score,
            snapshot.Reasons,
            snapshot.EligibleProducts.Select(ToResponse).ToArray(),
            snapshot.AssessedAt);

    private static CreditProfileSnapshotResponse ToResponse(
        this CreditProfileSnapshot snapshot) =>
        new(
            snapshot.ClientId,
            snapshot.CurrentScore,
            snapshot.CurrentResult,
            snapshot.CurrentEligibleProducts.Select(ToResponse).ToArray(),
            snapshot.LastAssessmentId,
            snapshot.Version,
            snapshot.UpdatedAt);

    private static CreditProductOfferResponse ToResponse(
        this CreditProductOffer offer) =>
        new(
            offer.ProductCode.ToString(),
            offer.Name,
            offer.MaxAmount.ToResponse(),
            offer.TermMonths,
            offer.MonthlyInterestRate);
}
