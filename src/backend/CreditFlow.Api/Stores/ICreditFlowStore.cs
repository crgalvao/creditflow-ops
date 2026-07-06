using CreditFlow.Domain.Applications;
using CreditFlow.Domain.CreditDecisioning;
using CreditFlow.Domain.Kyc;

namespace CreditFlow.Api.Stores;

public interface ICreditFlowStore
{
    public Task SaveApplicationAsync(
        LoanApplication application,
        CancellationToken cancellationToken);

    public Task<LoanApplication?> GetApplicationAsync(
        string applicationId,
        CancellationToken cancellationToken);

    public Task<IReadOnlyCollection<LoanApplication>> ListApplicationsAsync(
        CancellationToken cancellationToken);

    public Task SaveClientProfileAsync(
        ClientProfile clientProfile,
        CancellationToken cancellationToken);

    public Task<ClientProfile?> GetClientProfileByTaxIdAsync(
        string taxId,
        CancellationToken cancellationToken);

    public Task<ClientProfile?> GetClientProfileAsync(
        string clientId,
        CancellationToken cancellationToken);

    public Task SaveCreditAssessmentAsync(
        CreditAssessment assessment,
        CancellationToken cancellationToken);

    public Task<CreditAssessment?> GetCreditAssessmentByApplicationIdAsync(
        string applicationId,
        CancellationToken cancellationToken);

    public Task SaveCreditProfileAsync(
        CreditProfile creditProfile,
        CancellationToken cancellationToken);

    public Task<CreditProfile?> GetCreditProfileAsync(
        string clientId,
        CancellationToken cancellationToken);

    public Task AppendEventsAsync(
        IReadOnlyCollection<DomainEventRecord> events,
        CancellationToken cancellationToken);

    public Task<IReadOnlyCollection<DomainEventRecord>> GetEventsAsync(
        string applicationId,
        CancellationToken cancellationToken);
}
