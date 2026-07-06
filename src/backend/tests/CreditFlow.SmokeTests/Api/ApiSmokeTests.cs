using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CreditFlow.SmokeTests.Api;

public sealed class ApiSmokeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
            })
            .CreateClient();

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocument_ReturnsOk()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "application/json",
            response.Content.Headers.ContentType?.MediaType ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitApplication_WithValidRequest_ReturnsAcceptedAndApplicationId()
    {
        var request = new
        {
            ownerUserId = "smoke-user-001",
            borrower = new
            {
                legalName = "Smoke Test Trading Ltd",
                taxId = $"SMOKE-{Guid.NewGuid():N}",
                industry = "Wholesale",
                monthsInBusiness = 84
            },
            requestedAmount = 50_000m,
            monthlyRevenue = 90_000m,
            currency = "USD"
        };

        var response = await _client.PostAsJsonAsync("/applications", request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(responseStream);

        Assert.True(document.RootElement.TryGetProperty("applicationId", out var applicationId));
        Assert.False(string.IsNullOrWhiteSpace(applicationId.GetString()));
    }
}
