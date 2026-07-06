using CreditFlow.Api.Contracts.Applications;
using CreditFlow.Domain.Applications.Enums;
using CreditFlow.Domain.Kyc.Enums;

namespace CreditFlow.Api.Contracts.Kyc;

public sealed record CompleteKycRequest(
    bool MatchesSanctionList,
    bool MissingDocuments)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (MatchesSanctionList && MissingDocuments)
        {
            errors["kycOutcome"] = ["Use only one negative KYC outcome at a time."];
        }

        return errors;
    }
}

public sealed record KycResultResponse(
    string ApplicationId,
    ApplicationStatus ApplicationStatus,
    ApplicationKycState KycState,
    KycStatus KycStatus,
    string NextStep,
    KycSnapshotResponse Kyc);
