namespace CreditFlow.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseCreditFlowApi(this WebApplication app)
    {
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapGet("/", () => Results.Ok(new
        {
            service = "CreditFlow Ops API",
            status = "Running",
            openApi = app.Environment.IsDevelopment() ? "/openapi/v1.json" : null
        }))
        .WithName("GetApiRoot")
        .WithTags("System")
        .WithSummary("Returns basic API status metadata.");

        app.MapGet("/health", () => Results.Ok(new
        {
            status = "Healthy",
            checkedAt = DateTimeOffset.UtcNow
        }))
        .WithName("GetHealth")
        .WithTags("System")
        .WithSummary("Returns API health status.");

        return app;
    }
}
