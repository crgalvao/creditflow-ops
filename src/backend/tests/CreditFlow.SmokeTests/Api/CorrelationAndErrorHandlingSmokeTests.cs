using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CreditFlow.SmokeTests.Api;

public sealed class CorrelationAndErrorHandlingSmokeTests(WebApplicationFactory<Program> factory)
        : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
            })
            .CreateClient();

    [Fact]
    public async Task Health_WhenCorrelationIdIsProvided_EchoesCorrelationIdHeader()
    {
        const string correlationId = "smoke-correlation-001";

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-ID", correlationId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.Contains(correlationId, values);
    }

    [Fact]
    public async Task SubmitApplication_WhenRequestIsInvalid_ReturnsValidationProblemWithCorrelationHeader()
    {
        const string correlationId = "smoke-validation-001";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/applications");
        request.Headers.Add("X-Correlation-ID", correlationId);
        request.Content = JsonContent.Create(new
        {
            ownerUserId = "",
            borrower = new
            {
                legalName = "",
                taxId = "",
                industry = "",
                monthsInBusiness = 0
            },
            requestedAmount = 0m,
            monthlyRevenue = 0m,
            currency = ""
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.Contains(correlationId, values);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("errors", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreditAssessment_WhenWorkflowStateIsInvalid_ReturnsConflictProblemDetails()
    {
        const string correlationId = "smoke-workflow-conflict-001";

        var applicationId = await SubmitApplicationAsync(correlationId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/internal/credit/applications/{applicationId}/assess");

        request.Headers.Add("X-Correlation-ID", correlationId);
        request.Content = JsonContent.Create(new
        {
            assessmentId = (string?)null
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.Contains(correlationId, values);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Invalid workflow state", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(correlationId, body, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> SubmitApplicationAsync(string correlationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/applications");
        request.Headers.Add("X-Correlation-ID", correlationId);
        request.Content = JsonContent.Create(new
        {
            ownerUserId = "smoke-user-001",
            borrower = new
            {
                legalName = "Smoke Correlation Ltd",
                taxId = $"SMOKE-{Guid.NewGuid():N}",
                industry = "Wholesale",
                monthsInBusiness = 84
            },
            requestedAmount = 50_000m,
            monthlyRevenue = 90_000m,
            currency = "USD"
        });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        return document.RootElement
            .GetProperty("applicationId")
            .GetString()!;
    }
}
