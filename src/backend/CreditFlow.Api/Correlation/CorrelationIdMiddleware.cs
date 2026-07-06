using System.Diagnostics;
using CreditFlow.Api.Extensions;

namespace CreditFlow.Api.Correlation;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Items[CorrelationIdConstants.ItemKey] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdConstants.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using var activity = Activity.Current;
        activity?.SetTag("correlation.id", correlationId);

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        finally
        {
            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogHttpRequestCompleted(
                    context.Request.Method,
                    context.Request.Path.Value ?? "/",
                    context.Response.StatusCode,
                    elapsedMilliseconds);
            }
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(
                CorrelationIdConstants.HeaderName,
                out var values))
        {
            var candidate = values.FirstOrDefault();

            if (IsValidCorrelationId(candidate))
            {
                return candidate!.Trim();
            }
        }

        return Guid.CreateVersion7().ToString("N");
    }

    private static bool IsValidCorrelationId(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Length <= CorrelationIdConstants.MaxLength;
    }
}
