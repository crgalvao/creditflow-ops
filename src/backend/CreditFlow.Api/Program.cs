using CreditFlow.Infrastructure.DependencyInjection;
using System.Text.Json.Serialization;
using CreditFlow.Api.Correlation;
using CreditFlow.Api.Endpoints;
using CreditFlow.Api.Errors;
using CreditFlow.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var correlationId = context.HttpContext.GetOrCreateCorrelationId();

        context.ProblemDetails.Extensions["correlationId"] = correlationId;
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddSingleton<IUtcClock, SystemUtcClock>();
builder.Services.AddCreditFlowInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IDomainEventPublisher, InMemoryDomainEventPublisher>();
builder.Services.AddScoped<ApplicationWorkflowService>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => TypedResults.Ok(new
    {
        name = "CreditFlow Ops API",
        status = "Running"
    }))
    .WithName("Root")
    .WithTags("System")
    .WithSummary("Returns basic API status.");

app.MapGet("/health", (HttpContext httpContext, IUtcClock clock) => TypedResults.Ok(new
    {
        status = "Healthy",
        timestamp = clock.UtcNow,
        correlationId = httpContext.GetOrCreateCorrelationId()
    }))
    .WithName("Health")
    .WithTags("System")
    .WithSummary("Returns API health status.");

app.MapApplicationEndpoints();
app.MapKycEndpoints();
app.MapCreditDecisioningEndpoints();

app.Run();

public partial class Program;
