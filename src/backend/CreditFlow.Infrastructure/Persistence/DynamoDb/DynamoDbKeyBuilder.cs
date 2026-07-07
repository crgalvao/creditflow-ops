namespace CreditFlow.Infrastructure.Persistence.DynamoDb;

public static class DynamoDbKeyBuilder
{
    public const string PartitionKeyName = "PK";
    public const string SortKeyName = "SK";
    public const string Gsi1PartitionKeyName = "GSI1PK";
    public const string Gsi1SortKeyName = "GSI1SK";
    public const string Gsi2PartitionKeyName = "GSI2PK";
    public const string Gsi2SortKeyName = "GSI2SK";

    public static string ApplicationPartitionKey(string applicationId) =>
        $"APP#{NormalizeRequired(applicationId, nameof(applicationId))}";

    public static string ApplicationMetadataSortKey() => "METADATA";

    public static string ApplicationEventSortKey(DateTimeOffset occurredAt, Guid eventId) =>
        $"EVENT#{occurredAt.UtcDateTime:O}#{eventId:N}";

    public static string CreditAssessmentSortKey(string assessmentId) =>
        $"ASSESSMENT#{NormalizeRequired(assessmentId, nameof(assessmentId))}";

    public static string ClientPartitionKey(string clientId) =>
        $"CLIENT#{NormalizeRequired(clientId, nameof(clientId))}";

    public static string ClientProfileSortKey() => "PROFILE";

    public static string CreditProfileSortKey() => "CREDIT_PROFILE";

    public static string UserApplicationsPartitionKey(string ownerUserId) =>
        $"USER#{NormalizeRequired(ownerUserId, nameof(ownerUserId))}";

    public static string UserApplicationsSortKey(DateTimeOffset createdAt, string applicationId) =>
        $"CREATED#{createdAt.UtcDateTime:O}#APP#{NormalizeRequired(applicationId, nameof(applicationId))}";

    public static string StatusApplicationsPartitionKey(string status) =>
        $"STATUS#{NormalizeRequired(status, nameof(status))}";

    public static string StatusApplicationsSortKey(DateTimeOffset updatedAt, string applicationId) =>
        $"UPDATED#{updatedAt.UtcDateTime:O}#APP#{NormalizeRequired(applicationId, nameof(applicationId))}";

    public static string TaxIdPartitionKey(string taxId) =>
        $"TAX#{NormalizeRequired(taxId, nameof(taxId))}";

    public static string TaxIdSortKey(string clientId) =>
        $"CLIENT#{NormalizeRequired(clientId, nameof(clientId))}";

    public static string ClientAssessmentsPartitionKey(string clientId) =>
        $"CLIENT_ASSESSMENTS#{NormalizeRequired(clientId, nameof(clientId))}";

    public static string ClientAssessmentsSortKey(DateTimeOffset assessedAt, string assessmentId) =>
        $"ASSESSED#{assessedAt.UtcDateTime:O}#ASSESSMENT#{NormalizeRequired(assessmentId, nameof(assessmentId))}";

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Key value is required.", parameterName);
        }

        return value.Trim();
    }
}
