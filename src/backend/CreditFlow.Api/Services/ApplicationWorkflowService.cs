using CreditFlow.Api.Contracts.Applications;
using CreditFlow.Api.Contracts.CreditDecisioning;
using CreditFlow.Api.Contracts.Kyc;
using CreditFlow.Api.Errors;
using CreditFlow.Api.Mapping;
using CreditFlow.Api.Stores;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Applications.ValueObjects;
using CreditFlow.Domain.CreditDecisioning;
using CreditFlow.Domain.Kyc;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Api.Services;

public sealed class ApplicationWorkflowService(
    ICreditFlowStore store,
    IDomainEventPublisher eventPublisher,
    IUtcClock clock)
{
    public async Task<SubmitLoanApplicationResponse> SubmitApplicationAsync(
        CreateLoanApplicationRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var applicationId = $"app_{Guid.CreateVersion7():N}";

        var borrower = BorrowerSnapshot.Create(
            request.Borrower.LegalName,
            request.Borrower.TaxId,
            request.Borrower.Industry,
            request.Borrower.MonthsInBusiness);

        var requestedAmount = new Money(
            request.RequestedAmount,
            request.Currency);

        var monthlyRevenue = new Money(
            request.MonthlyRevenue,
            request.Currency);

        var application = LoanApplication.Submit(
            applicationId,
            request.OwnerUserId,
            borrower,
            requestedAmount,
            monthlyRevenue,
            now);

        await store.SaveApplicationAsync(application, cancellationToken);
        await PublishAndClearAsync(application, correlationId, cancellationToken);

        return application.ToSubmitResponse();
    }

    public Task<IReadOnlyCollection<LoanApplication>> ListApplicationsAsync(
        CancellationToken cancellationToken) =>
        store.ListApplicationsAsync(cancellationToken);

    public Task<LoanApplication?> GetApplicationAsync(
        string applicationId,
        CancellationToken cancellationToken) =>
        store.GetApplicationAsync(applicationId, cancellationToken);

    public async Task<bool> ApplicationExistsAsync(
        string applicationId,
        CancellationToken cancellationToken) =>
        await store.GetApplicationAsync(applicationId, cancellationToken) is not null;

    public Task<IReadOnlyCollection<DomainEventRecord>> GetTimelineAsync(
        string applicationId,
        CancellationToken cancellationToken) =>
        store.GetEventsAsync(applicationId, cancellationToken);

    public async Task<KycResultResponse?> CompleteKycAsync(
        string applicationId,
        CompleteKycRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var application = await store.GetApplicationAsync(applicationId, cancellationToken);

        if (application is null)
        {
            return null;
        }

        var now = clock.UtcNow;

        var clientProfile =
            await store.GetClientProfileByTaxIdAsync(application.Borrower.TaxId, cancellationToken) ??
            ClientProfile.Create(
                $"client_{Guid.CreateVersion7():N}",
                application.Borrower.TaxId,
                application.Borrower.LegalName,
                application.Borrower.Industry,
                now);

        clientProfile.UpsertFromBorrowerSnapshot(application.Borrower, now);

        var kycSnapshot = clientProfile.EvaluateKyc(
            request.MatchesSanctionList,
            request.MissingDocuments,
            now);

        application.RecordKycResult(kycSnapshot, now);

        await store.SaveClientProfileAsync(clientProfile, cancellationToken);
        await store.SaveApplicationAsync(application, cancellationToken);

        await PublishAndClearAsync(clientProfile, application.ApplicationId, correlationId, cancellationToken);
        await PublishAndClearAsync(application, correlationId, cancellationToken);

        return application.ToKycResultResponse(kycSnapshot);
    }

    public async Task<CreditAssessmentResultResponse?> CompleteCreditAssessmentAsync(
        string applicationId,
        CompleteCreditAssessmentRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var application = await store.GetApplicationAsync(applicationId, cancellationToken);

        if (application is null)
        {
            return null;
        }

        if (application.KycSnapshot is null)
        {
            throw new InvalidWorkflowStateException(
                "Cannot assess credit before KYC has been recorded.");
        }

        var now = clock.UtcNow;
        var assessmentId = string.IsNullOrWhiteSpace(request.AssessmentId)
            ? $"assess_{Guid.CreateVersion7():N}"
            : request.AssessmentId.Trim();

        var assessment = CreditDecisionRulesEngine.Assess(
            assessmentId,
            application.KycSnapshot.ClientId,
            application.ApplicationId,
            application.Borrower,
            application.RequestedAmount,
            application.MonthlyRevenue,
            application.KycSnapshot,
            now);

        application.RecordCreditAssessment(
            assessment.ToSnapshot(),
            now);

        await store.SaveCreditAssessmentAsync(assessment, cancellationToken);
        await store.SaveApplicationAsync(application, cancellationToken);

        await PublishAndClearAsync(assessment, application.ApplicationId, correlationId, cancellationToken);
        await PublishAndClearAsync(application, correlationId, cancellationToken);

        return application.ToCreditAssessmentResultResponse(assessment);
    }

    public async Task<CreditProfileUpsertResultResponse?> UpsertCreditProfileAndCompleteDecisionAsync(
        string applicationId,
        UpsertCreditProfileRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var application = await store.GetApplicationAsync(applicationId, cancellationToken);

        if (application is null)
        {
            return null;
        }

        var assessment = await store.GetCreditAssessmentByApplicationIdAsync(
            applicationId,
            cancellationToken);

        if (assessment is null)
        {
            throw new InvalidWorkflowStateException(
                "Cannot upsert credit profile before a credit assessment has been completed.");
        }

        var now = clock.UtcNow;

        var creditProfile =
            await store.GetCreditProfileAsync(assessment.ClientId, cancellationToken) ??
            CreditProfile.Create(assessment.ClientId);

        var creditProfileSnapshot = creditProfile.UpsertFromAssessment(
            assessment,
            now);

        application.CompleteDecision(
            creditProfileSnapshot,
            now);

        await store.SaveCreditProfileAsync(creditProfile, cancellationToken);
        await store.SaveApplicationAsync(application, cancellationToken);

        await PublishAndClearAsync(creditProfile, application.ApplicationId, correlationId, cancellationToken);
        await PublishAndClearAsync(application, correlationId, cancellationToken);

        return application.ToCreditProfileUpsertResultResponse(creditProfileSnapshot);
    }

    private async Task PublishAndClearAsync(
        LoanApplication application,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await eventPublisher.PublishAsync(
            application.ApplicationId,
            application.DomainEvents.ToArray(),
            correlationId,
            cancellationToken);

        application.ClearDomainEvents();
    }

    private async Task PublishAndClearAsync(
        AggregateRoot aggregate,
        string applicationId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await eventPublisher.PublishAsync(
            applicationId,
            aggregate.DomainEvents.ToArray(),
            correlationId,
            cancellationToken);

        aggregate.ClearDomainEvents();
    }
}
