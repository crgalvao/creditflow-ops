namespace CreditFlow.Api.Services;

public interface IUtcClock
{
    public DateTimeOffset UtcNow { get; }
}
