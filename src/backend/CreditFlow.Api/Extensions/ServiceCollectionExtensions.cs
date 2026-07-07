using System.Text.Json.Serialization;
using CreditFlow.Api.Errors;
using CreditFlow.Api.Services;
using CreditFlow.Infrastructure.Stores;

namespace CreditFlow.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCreditFlowApi(this IServiceCollection services)
    {
        services.AddOpenApi();
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        services.AddSingleton<ICreditFlowStore, InMemoryCreditFlowStore>();
        services.AddSingleton<IUtcClock, SystemUtcClock>();
        services.AddSingleton<IDomainEventPublisher, InMemoryDomainEventPublisher>();

        services.AddScoped<ApplicationWorkflowService>();

        return services;
    }
}
