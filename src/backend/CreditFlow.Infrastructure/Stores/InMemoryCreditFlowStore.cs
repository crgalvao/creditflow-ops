using System.Collections.Concurrent;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.CreditDecisioning;
using CreditFlow.Domain.Kyc;

namespace CreditFlow.Infrastructure.Stores;

public sealed class InMemoryCreditFlowStore : ICreditFlowStore
{
    private readonly ConcurrentDictionary<string, LoanApplication> _applications = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ClientProfile> _clientProfilesById = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _clientIdsByTaxId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CreditAssessment> _creditAssessmentsByApplicationId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CreditProfile> _creditProfilesByClientId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DomainEventRecord>> _eventsByApplicationId = new(StringComparer.Ordinal);

    public Task SaveApplicationAsync(LoanApplication application, CancellationToken cancellationToken)
    {
        _applications[application.ApplicationId] = application;
        return Task.CompletedTask;
    }

    public Task<LoanApplication?> GetApplicationAsync(string applicationId, CancellationToken cancellationToken)
    {
        _applications.TryGetValue(applicationId, out var application);
        return Task.FromResult(application);
    }

    public Task<IReadOnlyCollection<LoanApplication>> ListApplicationsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<LoanApplication> applications = _applications.Values
            .OrderByDescending(application => application.CreatedAt)
            .ToArray();

        return Task.FromResult(applications);
    }

    public Task SaveClientProfileAsync(ClientProfile clientProfile, CancellationToken cancellationToken)
    {
        _clientProfilesById[clientProfile.ClientId] = clientProfile;
        _clientIdsByTaxId[clientProfile.TaxId] = clientProfile.ClientId;
        return Task.CompletedTask;
    }

    public Task<ClientProfile?> GetClientProfileByTaxIdAsync(string taxId, CancellationToken cancellationToken)
    {
        if (!_clientIdsByTaxId.TryGetValue(taxId, out var clientId))
        {
            return Task.FromResult<ClientProfile?>(null);
        }

        _clientProfilesById.TryGetValue(clientId, out var clientProfile);
        return Task.FromResult(clientProfile);
    }

    public Task<ClientProfile?> GetClientProfileAsync(string clientId, CancellationToken cancellationToken)
    {
        _clientProfilesById.TryGetValue(clientId, out var clientProfile);
        return Task.FromResult(clientProfile);
    }

    public Task SaveCreditAssessmentAsync(CreditAssessment assessment, CancellationToken cancellationToken)
    {
        _creditAssessmentsByApplicationId[assessment.ApplicationId] = assessment;
        return Task.CompletedTask;
    }

    public Task<CreditAssessment?> GetCreditAssessmentByApplicationIdAsync(string applicationId, CancellationToken cancellationToken)
    {
        _creditAssessmentsByApplicationId.TryGetValue(applicationId, out var assessment);
        return Task.FromResult(assessment);
    }

    public Task SaveCreditProfileAsync(CreditProfile creditProfile, CancellationToken cancellationToken)
    {
        _creditProfilesByClientId[creditProfile.ClientId] = creditProfile;
        return Task.CompletedTask;
    }

    public Task<CreditProfile?> GetCreditProfileAsync(string clientId, CancellationToken cancellationToken)
    {
        _creditProfilesByClientId.TryGetValue(clientId, out var creditProfile);
        return Task.FromResult(creditProfile);
    }

    public Task AppendEventsAsync(IReadOnlyCollection<DomainEventRecord> events, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in events)
        {
            var queue = _eventsByApplicationId.GetOrAdd(
                domainEvent.ApplicationId,
                _ => new ConcurrentQueue<DomainEventRecord>());

            queue.Enqueue(domainEvent);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<DomainEventRecord>> GetEventsAsync(string applicationId, CancellationToken cancellationToken)
    {
        if (!_eventsByApplicationId.TryGetValue(applicationId, out var queue))
        {
            return Task.FromResult<IReadOnlyCollection<DomainEventRecord>>([]);
        }

        IReadOnlyCollection<DomainEventRecord> events = queue
            .OrderBy(domainEvent => domainEvent.OccurredAt)
            .ToArray();

        return Task.FromResult(events);
    }
}
