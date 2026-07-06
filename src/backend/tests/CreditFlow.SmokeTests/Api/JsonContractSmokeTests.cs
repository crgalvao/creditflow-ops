using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CreditFlow.SmokeTests.Api;

public sealed class JsonContractSmokeTests(WebApplicationFactory<Program> factory)
        : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
            })
            .CreateClient();

    [Fact]
    public async Task SubmitApplication_ReturnsEnumValuesAsStrings()
    {
        var request = new
        {
            ownerUserId = "json-contract-user-001",
            borrower = new
            {
                legalName = "Json Contract Trading Ltd",
                taxId = $"JSON-{Guid.NewGuid():N}",
                industry = "Wholesale",
                monthsInBusiness = 84
            },
            requestedAmount = 50_000m,
            monthlyRevenue = 90_000m,
            currency = "USD"
        };

        var response = await _client.PostAsJsonAsync("/applications", request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        var root = document.RootElement;

        Assert.Equal(JsonValueKind.String, root.GetProperty("status").ValueKind);
        Assert.Equal(JsonValueKind.String, root.GetProperty("kycState").ValueKind);
        Assert.Equal(JsonValueKind.String, root.GetProperty("creditState").ValueKind);

        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("status").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("kycState").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("creditState").GetString()));
    }
}
