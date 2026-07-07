using Amazon;
using Amazon.DynamoDBv2;
using CreditFlow.Infrastructure.Persistence.DynamoDb;
using CreditFlow.Infrastructure.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CreditFlow.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddCreditFlowInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var storeProvider = configuration["CreditFlow:Store"] ??
                            Environment.GetEnvironmentVariable("CREDITFLOW_STORE") ??
                            "InMemory";

        if (string.Equals(storeProvider, "DynamoDb", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton(CreateDynamoDbStoreOptions(configuration));
            services.AddSingleton(CreateDynamoDbClient);
            services.AddSingleton<ICreditFlowStore, DynamoDbCreditFlowStore>();
        }
        else
        {
            services.AddSingleton<ICreditFlowStore, InMemoryCreditFlowStore>();
        }

        return services;
    }

    private static IOptions<DynamoDbStoreOptions> CreateDynamoDbStoreOptions(IConfiguration configuration)
    {
        var options = new DynamoDbStoreOptions
        {
            TableName = configuration["DynamoDb:TableName"] ??
                        Environment.GetEnvironmentVariable("DYNAMODB_TABLE_NAME") ??
                        "CreditFlowLocal",

            Region = configuration["DynamoDb:Region"] ??
                     Environment.GetEnvironmentVariable("AWS_REGION") ??
                     "us-east-1",

            ServiceUrl = configuration["DynamoDb:ServiceUrl"] ??
                         Environment.GetEnvironmentVariable("DYNAMODB_SERVICE_URL"),

            AccessKeyId = configuration["DynamoDb:AccessKeyId"] ??
                          Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ??
                          "test",

            SecretAccessKey = configuration["DynamoDb:SecretAccessKey"] ??
                              Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ??
                              "test"
        };

        return Options.Create(options);
    }

    private static IAmazonDynamoDB CreateDynamoDbClient(IServiceProvider serviceProvider)
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<DynamoDbStoreOptions>>()
            .Value;

        var config = new AmazonDynamoDBConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region)
        };

        if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            config.ServiceURL = options.ServiceUrl;
            config.AuthenticationRegion = options.Region;

            return new AmazonDynamoDBClient(
                options.AccessKeyId,
                options.SecretAccessKey,
                config);
        }

        return new AmazonDynamoDBClient(config);
    }
}
