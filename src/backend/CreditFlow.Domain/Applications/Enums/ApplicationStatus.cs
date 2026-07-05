namespace CreditFlow.Domain.Applications.Enums;

public enum ApplicationStatus
{
    Submitted = 1,
    KycInProgress = 2,
    CreditInProgress = 3,
    Approved = 4,
    ManualReview = 5,
    Rejected = 6,
    Failed = 7
}
