namespace CreditFlow.Api.Services;

public sealed class SystemUtcClock : IUtcClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
