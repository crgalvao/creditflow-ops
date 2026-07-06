namespace CreditFlow.Api.Correlation;

public static class CorrelationIdConstants
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CreditFlow.CorrelationId";
    public const int MaxLength = 128;
}
