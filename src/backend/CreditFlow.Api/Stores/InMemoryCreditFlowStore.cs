using System.Collections.Concurrent;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.CreditDecisioning;
using CreditFlow.Domain.Kyc;

namespace CreditFlow.Api.Stores;

public sealed class InMemoryCreditFlowStore : ICreditFlowStore
{
    private readonly ConcurrentDictionary<string, LoanApplication> _applications = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ClientProfile> _clientProfilesById = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _clientProfileIdByTaxId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CreditAssessment> _creditAssessmentsByApplicationId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CreditProfile> _creditProfilesByClientId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, List<DomainEventRecord>> _eventsByApplicationId = new(StringComparer.Ordinal);

    public Task SaveApplicationAsync(
        LoanApplication application,
        CancellationToken cancellationToken)
    {
        _applications[application.ApplicationId] = application;

        return Task.CompletedTask;
    }

    public Task<LoanApplication?> GetApplicationAsync(
        string applicationId,
        CancellationToken cancellationToken)
    {
        _applications.TryGetValue(applicationId, out var application);

        return Task.FromResult(application);
    }

    public Task<IReadOnlyCollection<LoanApplication>> ListApplicationsAsync(
        CancellationToken cancellationToken)
    {
        var applications = _applications.Values
            .OrderByDescending(application => application.CreatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<LoanApplication>>(applications);
    }

    public Task SaveClientProfileAsync(
        ClientProfile clientProfile,
        CancellationToken cancellationToken)
    {
        _clientProfilesById[clientProfile.ClientId] = clientProfile;
        _clientProfileIdByTaxId[clientProfile.TaxId] = clientProfile.ClientId;

        return Task.CompletedTask;
    }

    public Task<ClientProfile?> GetClientProfileByTaxIdAsync(
        string taxId,
        CancellationToken cancellationToken)
    {
        if (_clientProfileIdByTaxId.TryGetValue(taxId, out var clientId) &&
            _clientProfilesById.TryGetValue(clientId, out var clientProfile))
        {
            return Task.FromResult<ClientProfile?>(clientProfile);
        }

        return Task.FromResult<ClientProfile?>(null);
    }

    public Task<ClientProfile?> GetClientProfileAsync(
        string clientId,
        CancellationToken cancellationToken)
    {
        _clientProfilesById.TryGetValue(clientId, out var clientProfile);

        return Task.FromResult(clientProfile);
    }

    public Task SaveCreditAssessmentAsync(
        CreditAssessment assessment,
        CancellationToken cancellationToken)
    {
        _creditAssessmentsByApplicationId[assessment.ApplicationId] = assessment;

        return Task.CompletedTask;
    }

    public Task<CreditAssessment?> GetCreditAssessmentByApplicationIdAsync(
        string applicationId,
        CancellationToken cancellationToken)
    {
        _creditAssessmentsByApplicationId.TryGetValue(applicationId, out var assessment);

        return Task.FromResult(assessment);
    }

    public Task SaveCreditProfileAsync(
        CreditProfile creditProfile,
        CancellationToken cancellationToken)
    {
        _creditProfilesByClientId[creditProfile.ClientId] = creditProfile;

        return Task.CompletedTask;
    }

    public Task<CreditProfile?> GetCreditProfileAsync(
        string clientId,
        CancellationToken cancellationToken)
    {
        _creditProfilesByClientId.TryGetValue(clientId, out var creditProfile);

        return Task.FromResult(creditProfile);
    }

    public Task AppendEventsAsync(
        IReadOnlyCollection<DomainEventRecord> events,
        CancellationToken cancellationToken)
    {
        foreach (var domainEvent in events)
        {
            var timeline = _eventsByApplicationId.GetOrAdd(domainEvent.ApplicationId, _ => []);

            lock (timeline)
            {
                timeline.Add(domainEvent);
                timeline.Sort(static (left, right) => left.OccurredAt.CompareTo(right.OccurredAt));
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<DomainEventRecord>> GetEventsAsync(
        string applicationId,
        CancellationToken cancellationToken)
    {
        if (!_eventsByApplicationId.TryGetValue(applicationId, out var events))
        {
            return Task.FromResult<IReadOnlyCollection<DomainEventRecord>>([]);
        }

        lock (events)
        {
            return Task.FromResult<IReadOnlyCollection<DomainEventRecord>>(
                events.OrderBy(domainEvent => domainEvent.OccurredAt).ToArray());
        }
    }
}
