namespace CreditFlow.Infrastructure.Persistence.DynamoDb;

public sealed class DynamoDbStoreOptions
{
    public string TableName { get; init; } = "CreditFlowLocal";
    public string Region { get; init; } = "us-east-1";
    public string? ServiceUrl { get; init; }
    public string AccessKeyId { get; init; } = "test";
    public string SecretAccessKey { get; init; } = "test";
}
