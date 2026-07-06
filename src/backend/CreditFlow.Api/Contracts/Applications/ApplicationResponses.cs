using CreditFlow.Domain.Applications.Enums;
using CreditFlow.Domain.CreditDecisioning.Enums;
using CreditFlow.Domain.Kyc.Enums;

namespace CreditFlow.Api.Contracts.Applications;

public sealed record SubmitLoanApplicationResponse(
    string ApplicationId,
    ApplicationStatus Status,
    ApplicationKycState KycState,
    ApplicationCreditState CreditState,
    string NextStep,
    DateTimeOffset CreatedAt);

public sealed record LoanApplicationSummaryResponse(
    string ApplicationId,
    string OwnerUserId,
    string BorrowerLegalName,
    string BorrowerTaxId,
    decimal RequestedAmount,
    string Currency,
    ApplicationStatus Status,
    ApplicationKycState KycState,
    ApplicationCreditState CreditState,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record LoanApplicationDetailsResponse(
    string ApplicationId,
    string OwnerUserId,
    BorrowerResponse Borrower,
    MoneyResponse RequestedAmount,
    MoneyResponse MonthlyRevenue,
    ApplicationStatus Status,
    ApplicationKycState KycState,
    ApplicationCreditState CreditState,
    KycSnapshotResponse? Kyc,
    CreditAssessmentSnapshotResponse? CreditAssessment,
    CreditProfileSnapshotResponse? CreditProfile,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record LoanDecisionResponse(
    string ApplicationId,
    ApplicationStatus Status,
    ApplicationKycState KycState,
    ApplicationCreditState CreditState,
    KycSnapshotResponse? Kyc,
    CreditAssessmentSnapshotResponse? CreditAssessment,
    CreditProfileSnapshotResponse? CreditProfile,
    DateTimeOffset UpdatedAt);

public sealed record BorrowerResponse(
    string LegalName,
    string TaxId,
    string Industry,
    int MonthsInBusiness);

public sealed record MoneyResponse(
    decimal Amount,
    string Currency);

public sealed record KycSnapshotResponse(
    string ClientId,
    string TaxId,
    string LegalName,
    KycStatus Status,
    IReadOnlyCollection<string> RiskFlags,
    int Version,
    DateTimeOffset CheckedAt);

public sealed record CreditAssessmentSnapshotResponse(
    string AssessmentId,
    string ClientId,
    string ApplicationId,
    CreditAssessmentResult Result,
    int Score,
    IReadOnlyCollection<string> Reasons,
    IReadOnlyCollection<CreditProductOfferResponse> EligibleProducts,
    DateTimeOffset AssessedAt);

public sealed record CreditProfileSnapshotResponse(
    string ClientId,
    int CurrentScore,
    CreditAssessmentResult CurrentResult,
    IReadOnlyCollection<CreditProductOfferResponse> CurrentEligibleProducts,
    string LastAssessmentId,
    int Version,
    DateTimeOffset UpdatedAt);

public sealed record CreditProductOfferResponse(
    string ProductCode,
    string Name,
    MoneyResponse MaxAmount,
    int TermMonths,
    decimal MonthlyInterestRate);

public sealed record DomainEventResponse(
    Guid EventId,
    string EventType,
    int EventVersion,
    string CorrelationId,
    DateTimeOffset OccurredAt,
    string Source,
    object Data);
