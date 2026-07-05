namespace CreditFlow.Domain.SharedKernel;

public interface IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAt { get; }
}
