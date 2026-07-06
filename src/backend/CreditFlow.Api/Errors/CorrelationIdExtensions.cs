namespace CreditFlow.Api.Errors;

public static class CorrelationIdExtensions
{
    public const string HeaderName = "X-Correlation-Id";

    public static string GetOrCreateCorrelationId(this HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue(HeaderName, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return value.ToString();
        }

        var correlationId = $"corr_{Guid.CreateVersion7():N}";
        httpContext.Response.Headers[HeaderName] = correlationId;

        return correlationId;
    }
}
