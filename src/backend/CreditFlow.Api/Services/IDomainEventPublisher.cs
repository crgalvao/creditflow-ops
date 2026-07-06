using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Api.Services;

public interface IDomainEventPublisher
{
    public Task PublishAsync(
        string applicationId,
        IReadOnlyCollection<IDomainEvent> domainEvents,
        string correlationId,
        CancellationToken cancellationToken);
}
