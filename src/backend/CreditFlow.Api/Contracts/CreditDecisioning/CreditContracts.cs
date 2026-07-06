using CreditFlow.Api.Contracts.Applications;
using CreditFlow.Domain.Applications.Enums;
using CreditFlow.Domain.CreditDecisioning.Enums;

namespace CreditFlow.Api.Contracts.CreditDecisioning;

public sealed record CompleteCreditAssessmentRequest(
    string? AssessmentId)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (AssessmentId is not null && string.IsNullOrWhiteSpace(AssessmentId))
        {
            errors[nameof(AssessmentId)] = ["Assessment id cannot be blank when provided."];
        }

        return errors;
    }
}

public sealed record CreditAssessmentResultResponse(
    string ApplicationId,
    ApplicationStatus ApplicationStatus,
    ApplicationCreditState CreditState,
    CreditAssessmentResult Result,
    int Score,
    string NextStep,
    CreditAssessmentSnapshotResponse CreditAssessment);

public sealed record UpsertCreditProfileRequest
{
    public Dictionary<string, string[]> Validate() => [];
}

public sealed record CreditProfileUpsertResultResponse(
    string ApplicationId,
    ApplicationStatus FinalStatus,
    CreditProfileSnapshotResponse CreditProfile,
    LoanDecisionResponse Decision);
