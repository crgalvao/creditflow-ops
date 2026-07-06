using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace CreditFlow.SmokeTests.Api;

public sealed class CorrelationCompletionLogSmokeTests
{
    [Fact]
    public async Task WorkflowConflict_IsLoggedWithFinalConflictStatusCode()
    {
        var logCollector = new TestLogCollector();

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");

                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(new TestLoggerProvider(logCollector));
                    logging.SetMinimumLevel(LogLevel.Information);
                });
            });

        using var client = factory.CreateClient();

        const string correlationId = "smoke-final-status-log-001";
        var applicationId = await SubmitApplicationAsync(client, correlationId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/internal/credit/applications/{applicationId}/assess");

        request.Headers.Add("X-Correlation-ID", correlationId);
        request.Content = JsonContent.Create(new
        {
            assessmentId = (string?)null
        });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var completionLog = logCollector.Entries
            .Where(entry =>
                entry.Level == LogLevel.Information &&
                entry.Category.Contains("CorrelationIdMiddleware", StringComparison.Ordinal) &&
                entry.Message.Contains("responded", StringComparison.OrdinalIgnoreCase) &&
                entry.Message.Contains($"/internal/credit/applications/{applicationId}/assess", StringComparison.Ordinal))
            .LastOrDefault();

        Assert.NotNull(completionLog);

        Assert.True(
            completionLog!.HasStructuredValue("StatusCode", 409) ||
            completionLog.Message.Contains("responded 409", StringComparison.Ordinal),
            $"Expected request completion log to contain final status code 409. Actual message: {completionLog.Message}");

        Assert.False(
            completionLog.HasStructuredValue("StatusCode", 200) ||
            completionLog.Message.Contains("responded 200", StringComparison.Ordinal),
            $"Request completion log must not record 200 for handled workflow exceptions. Actual message: {completionLog.Message}");
    }

    private static async Task<string> SubmitApplicationAsync(
        HttpClient client,
        string correlationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/applications");
        request.Headers.Add("X-Correlation-ID", correlationId);
        request.Content = JsonContent.Create(new
        {
            ownerUserId = "smoke-user-001",
            borrower = new
            {
                legalName = "Final Status Log Ltd",
                taxId = $"LOG-{Guid.NewGuid():N}",
                industry = "Wholesale",
                monthsInBusiness = 84
            },
            requestedAmount = 50_000m,
            monthlyRevenue = 90_000m,
            currency = "USD"
        });

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        return document.RootElement
            .GetProperty("applicationId")
            .GetString()!;
    }

    private sealed class TestLogCollector
    {
        public ConcurrentQueue<TestLogEntry> Entries { get; } = new();
    }

    private sealed class TestLoggerProvider(TestLogCollector collector) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(categoryName, collector);
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestLogger(
        string categoryName,
        TestLogCollector collector) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = ExtractProperties(state);

            collector.Entries.Enqueue(new TestLogEntry(
                Category: categoryName,
                Level: logLevel,
                EventId: eventId,
                Message: formatter(state, exception),
                Properties: properties));
        }

        private static Dictionary<string, object?> ExtractProperties<TState>(
            TState state)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> values)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal);
            }

            return values
                .Where(value => value.Key != "{OriginalFormat}")
                .ToDictionary(
                    value => value.Key,
                    value => value.Value,
                    StringComparer.Ordinal);
        }
    }

    private sealed record TestLogEntry(
        string Category,
        LogLevel Level,
        EventId EventId,
        string Message,
        IReadOnlyDictionary<string, object?> Properties)
    {
        public bool HasStructuredValue(string key, int expectedValue)
        {
            if (!Properties.TryGetValue(key, out var value))
            {
                return false;
            }

            return value switch
            {
                int intValue => intValue == expectedValue,
                long longValue => longValue == expectedValue,
                string stringValue when int.TryParse(stringValue, out var parsed) => parsed == expectedValue,
                _ => false
            };
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
