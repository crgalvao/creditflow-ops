using CreditFlow.Api.Correlation;

namespace CreditFlow.Api.Errors;

public static class HttpContextCorrelationExtensions
{
    public static string GetOrCreateCorrelationId(this HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(CorrelationIdConstants.ItemKey, out var value) &&
            value is string existingCorrelationId &&
            !string.IsNullOrWhiteSpace(existingCorrelationId))
        {
            return existingCorrelationId;
        }

        if (httpContext.Request.Headers.TryGetValue(
                CorrelationIdConstants.HeaderName,
                out var values))
        {
            var candidate = values.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(candidate) &&
                candidate.Length <= CorrelationIdConstants.MaxLength)
            {
                var correlationId = candidate.Trim();
                httpContext.Items[CorrelationIdConstants.ItemKey] = correlationId;
                return correlationId;
            }
        }

        var generatedCorrelationId = Guid.CreateVersion7().ToString("N");
        httpContext.Items[CorrelationIdConstants.ItemKey] = generatedCorrelationId;

        return generatedCorrelationId;
    }
}
