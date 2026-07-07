using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Applications.Enums;
using CreditFlow.Domain.Applications.ValueObjects;
using CreditFlow.Domain.CreditDecisioning;
using CreditFlow.Domain.CreditDecisioning.Enums;
using CreditFlow.Domain.CreditDecisioning.ValueObjects;
using CreditFlow.Domain.Kyc;
using CreditFlow.Domain.Kyc.ValueObjects;
using CreditFlow.Domain.SharedKernel;
using CreditFlow.Infrastructure.Stores;
using Microsoft.Extensions.Options;

namespace CreditFlow.Infrastructure.Persistence.DynamoDb;

public sealed class DynamoDbCreditFlowStore(
    IAmazonDynamoDB dynamoDb,
    IOptions<DynamoDbStoreOptions> options) : ICreditFlowStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly string _tableName = string.IsNullOrWhiteSpace(options.Value.TableName)
        ? throw new InvalidOperationException("DynamoDB table name is required.")
        : options.Value.TableName;

    public async Task SaveApplicationAsync(LoanApplication application, CancellationToken cancellationToken)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            [DynamoDbKeyBuilder.PartitionKeyName] = S(DynamoDbKeyBuilder.ApplicationPartitionKey(application.ApplicationId)),
            [DynamoDbKeyBuilder.SortKeyName] = S(DynamoDbKeyBuilder.ApplicationMetadataSortKey()),
            ["EntityType"] = S("LoanApplication"),
            ["ApplicationId"] = S(application.ApplicationId),
            ["OwnerUserId"] = S(application.OwnerUserId),
            ["BorrowerLegalName"] = S(application.Borrower.LegalName),
            ["BorrowerTaxId"] = S(application.Borrower.TaxId),
            ["BorrowerIndustry"] = S(application.Borrower.Industry),
            ["BorrowerMonthsInBusiness"] = N(application.Borrower.MonthsInBusiness),
            ["RequestedAmount"] = N(application.RequestedAmount.Amount),
            ["RequestedAmountCurrency"] = S(application.RequestedAmount.Currency),
            ["MonthlyRevenue"] = N(application.MonthlyRevenue.Amount),
            ["MonthlyRevenueCurrency"] = S(application.MonthlyRevenue.Currency),
            ["Status"] = S(application.Status.ToString()),
            ["KycState"] = S(application.KycState.ToString()),
            ["CreditState"] = S(application.CreditState.ToString()),
            ["CreatedAt"] = S(application.CreatedAt),
            ["UpdatedAt"] = S(application.UpdatedAt),
            [DynamoDbKeyBuilder.Gsi1PartitionKeyName] = S(DynamoDbKeyBuilder.UserApplicationsPartitionKey(application.OwnerUserId)),
            [DynamoDbKeyBuilder.Gsi1SortKeyName] = S(DynamoDbKeyBuilder.UserApplicationsSortKey(application.CreatedAt, application.ApplicationId)),
            [DynamoDbKeyBuilder.Gsi2PartitionKeyName] = S(DynamoDbKeyBuilder.StatusApplicationsPartitionKey(application.Status.ToString())),
            [DynamoDbKeyBuilder.Gsi2SortKeyName] = S(DynamoDbKeyBuilder.StatusApplicationsSortKey(application.UpdatedAt, application.ApplicationId))
        };

        AddJson(item, "KycSnapshot", application.KycSnapshot);
        AddJson(item, "CreditAssessmentSnapshot", application.CreditAssessmentSnapshot);
        AddJson(item, "CreditProfileSnapshot", application.CreditProfileSnapshot);

        await dynamoDb.PutItemAsync(_tableName, item, cancellationToken);
    }

    public async Task<LoanApplication?> GetApplicationAsync(string applicationId, CancellationToken cancellationToken)
    {
        var response = await dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoDbKeyBuilder.PartitionKeyName] = S(DynamoDbKeyBuilder.ApplicationPartitionKey(applicationId)),
                [DynamoDbKeyBuilder.SortKeyName] = S(DynamoDbKeyBuilder.ApplicationMetadataSortKey())
            }
        }, cancellationToken);

        return response.Item.Count == 0 ? null : ToLoanApplication(response.Item);
    }

    public async Task<IReadOnlyCollection<LoanApplication>> ListApplicationsAsync(CancellationToken cancellationToken)
    {
        var applications = new List<LoanApplication>();
        Dictionary<string, AttributeValue>? lastEvaluatedKey = null;

        do
        {
            var response = await dynamoDb.ScanAsync(new ScanRequest
            {
                TableName = _tableName,
                ExclusiveStartKey = lastEvaluatedKey,
                FilterExpression = "EntityType = :entityType",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":entityType"] = S("LoanApplication")
                }
            }, cancellationToken);

            applications.AddRange(response.Items.Select(ToLoanApplication));
            lastEvaluatedKey = response.LastEvaluatedKey;
        }
        while (lastEvaluatedKey is { Count: > 0 });

        return applications
            .OrderByDescending(application => application.CreatedAt)
            .ToArray();
    }

    public async Task SaveClientProfileAsync(ClientProfile clientProfile, CancellationToken cancellationToken)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            [DynamoDbKeyBuilder.PartitionKeyName] = S(DynamoDbKeyBuilder.ClientPartitionKey(clientProfile.ClientId)),
            [DynamoDbKeyBuilder.SortKeyName] = S(DynamoDbKeyBuilder.ClientProfileSortKey()),
            ["EntityType"] = S("ClientProfile"),
            ["ClientId"] = S(clientProfile.ClientId),
            ["TaxId"] = S(clientProfile.TaxId),
            ["LegalName"] = S(clientProfile.LegalName),
            ["Industry"] = S(clientProfile.Industry),
            ["Status"] = S(clientProfile.Status.ToString()),
            ["KycStatus"] = S(clientProfile.KycStatus.ToString()),
            ["Version"] = N(clientProfile.Version),
            ["CreatedAt"] = S(clientProfile.CreatedAt),
            ["UpdatedAt"] = S(clientProfile.UpdatedAt),
            [DynamoDbKeyBuilder.Gsi1PartitionKeyName] = S(DynamoDbKeyBuilder.TaxIdPartitionKey(clientProfile.TaxId)),
            [DynamoDbKeyBuilder.Gsi1SortKeyName] = S(DynamoDbKeyBuilder.TaxIdSortKey(clientProfile.ClientId))
        };

        AddJson(item, "RiskFlags", clientProfile.RiskFlags);

        await dynamoDb.PutItemAsync(_tableName, item, cancellationToken);
    }

    public async Task<ClientProfile?> GetClientProfileByTaxIdAsync(string taxId, CancellationToken cancellationToken)
    {
        var response = await dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :pk AND begins_with(GSI1SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = S(DynamoDbKeyBuilder.TaxIdPartitionKey(taxId)),
                [":prefix"] = S("CLIENT#")
            },
            Limit = 1
        }, cancellationToken);

        return response.Items.Count == 0 ? null : ToClientProfile(response.Items[0]);
    }

    public async Task<ClientProfile?> GetClientProfileAsync(string clientId, CancellationToken cancellationToken)
    {
        var response = await dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoDbKeyBuilder.PartitionKeyName] = S(DynamoDbKeyBuilder.ClientPartitionKey(clientId)),
                [DynamoDbKeyBuilder.SortKeyName] = S(DynamoDbKeyBuilder.ClientProfileSortKey())
            }
        }, cancellationToken);

        return response.Item.Count == 0 ? null : ToClientProfile(response.Item);
    }

    public async Task SaveCreditAssessmentAsync(CreditAssessment assessment, CancellationToken cancellationToken)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            [DynamoDbKeyBuilder.PartitionKeyName] = S(DynamoDbKeyBuilder.ApplicationPartitionKey(assessment.ApplicationId)),
            [DynamoDbKeyBuilder.SortKeyName] = S(DynamoDbKeyBuilder.CreditAssessmentSortKey(assessment.AssessmentId)),
            ["EntityType"] = S("CreditAssessment"),
            ["AssessmentId"] = S(assessment.AssessmentId),
            ["ApplicationId"] = S(assessment.ApplicationId),
            ["ClientId"] = S(assessment.ClientId),
            ["Result"] = S(assessment.Result.ToString()),
            ["Score"] = N(assessment.Score),
            ["AssessedAt"] = S(assessment.AssessedAt),
            [DynamoDbKeyBuilder.Gsi1PartitionKeyName] = S(DynamoDbKeyBuilder.ClientAssessmentsPartitionKey(assessment.ClientId)),
            [DynamoDbKeyBuilder.Gsi1SortKeyName] = S(DynamoDbKeyBuilder.ClientAssessmentsSortKey(assessment.AssessedAt, assessment.AssessmentId))
        };

        AddJson(item, "Reasons", assessment.Reasons);
        AddJson(item, "EligibleProducts", assessment.EligibleProducts);

        await dynamoDb.PutItemAsync(_tableName, item, cancellationToken);
    }

    public async Task<CreditAssessment?> GetCreditAssessmentByApplicationIdAsync(string applicationId, CancellationToken cancellationToken)
    {
        var response = await dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = S(DynamoDbKeyBuilder.ApplicationPartitionKey(applicationId)),
                [":prefix"] = S("ASSESSMENT#")
            },
            Limit = 1,
            ScanIndexForward = false
        }, cancellationToken);

        return response.Items.Count == 0 ? null : ToCreditAssessment(response.Items[0]);
    }

    public async Task SaveCreditProfileAsync(CreditProfile creditProfile, CancellationToken cancellationToken)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            [DynamoDbKeyBuilder.PartitionKeyName] = S(DynamoDbKeyBuilder.ClientPartitionKey(creditProfile.ClientId)),
            [DynamoDbKeyBuilder.SortKeyName] = S(DynamoDbKeyBuilder.CreditProfileSortKey()),
            ["EntityType"] = S("CreditProfile"),
            ["ClientId"] = S(creditProfile.ClientId),
            ["CurrentScore"] = N(creditProfile.CurrentScore),
            ["CurrentResult"] = S(creditProfile.CurrentResult.ToString()),
            ["LastAssessmentId"] = S(creditProfile.LastAssessmentId),
            ["Version"] = N(creditProfile.Version),
            ["UpdatedAt"] = S(creditProfile.UpdatedAt)
        };

        AddJson(item, "CurrentEligibleProducts", creditProfile.CurrentEligibleProducts);

        await dynamoDb.PutItemAsync(_tableName, item, cancellationToken);
    }

    public async Task<CreditProfile?> GetCreditProfileAsync(string clientId, CancellationToken cancellationToken)
    {
        var response = await dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoDbKeyBuilder.PartitionKeyName] = S(DynamoDbKeyBuilder.ClientPartitionKey(clientId)),
                [DynamoDbKeyBuilder.SortKeyName] = S(DynamoDbKeyBuilder.CreditProfileSortKey())
            }
        }, cancellationToken);

        return response.Item.Count == 0 ? null : ToCreditProfile(response.Item);
    }

    public async Task AppendEventsAsync(IReadOnlyCollection<DomainEventRecord> events, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in events)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                [DynamoDbKeyBuilder.PartitionKeyName] = S(DynamoDbKeyBuilder.ApplicationPartitionKey(domainEvent.ApplicationId)),
                [DynamoDbKeyBuilder.SortKeyName] = S(DynamoDbKeyBuilder.ApplicationEventSortKey(domainEvent.OccurredAt, domainEvent.EventId)),
                ["EntityType"] = S("DomainEvent"),
                ["ApplicationId"] = S(domainEvent.ApplicationId),
                ["EventId"] = S(domainEvent.EventId),
                ["EventType"] = S(domainEvent.EventType),
                ["OccurredAt"] = S(domainEvent.OccurredAt),
                ["CorrelationId"] = S(domainEvent.CorrelationId),
                ["EventVersion"] = N(domainEvent.EventVersion),
                ["Source"] = S(domainEvent.Source),
                ["Data"] = S(JsonSerializer.Serialize(domainEvent.Data, JsonOptions))
            };

            await dynamoDb.PutItemAsync(_tableName, item, cancellationToken);
        }
    }

    public async Task<IReadOnlyCollection<DomainEventRecord>> GetEventsAsync(string applicationId, CancellationToken cancellationToken)
    {
        var response = await dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = S(DynamoDbKeyBuilder.ApplicationPartitionKey(applicationId)),
                [":prefix"] = S("EVENT#")
            },
            ScanIndexForward = true
        }, cancellationToken);

        return response.Items
            .Select(ToDomainEventRecord)
            .OrderBy(domainEvent => domainEvent.OccurredAt)
            .ToArray();
    }

    private static LoanApplication ToLoanApplication(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var application = LoanApplication.Submit(
            GetString(item, "ApplicationId"),
            GetString(item, "OwnerUserId"),
            BorrowerSnapshot.Create(
                GetString(item, "BorrowerLegalName"),
                GetString(item, "BorrowerTaxId"),
                GetString(item, "BorrowerIndustry"),
                GetInt(item, "BorrowerMonthsInBusiness")),
            new Money(GetDecimal(item, "RequestedAmount"), GetString(item, "RequestedAmountCurrency")),
            new Money(GetDecimal(item, "MonthlyRevenue"), GetString(item, "MonthlyRevenueCurrency")),
            GetDateTimeOffset(item, "CreatedAt"));

        application.ClearDomainEvents();

        var status = GetEnum<ApplicationStatus>(item, "Status");
        var kycSnapshot = GetJson<KycSnapshot>(item, "KycSnapshot");
        var creditAssessmentSnapshot = GetJson<CreditAssessmentSnapshot>(item, "CreditAssessmentSnapshot");
        var creditProfileSnapshot = GetJson<CreditProfileSnapshot>(item, "CreditProfileSnapshot");

        if (kycSnapshot is not null)
        {
            application.RecordKycResult(kycSnapshot, kycSnapshot.CheckedAt);
            application.ClearDomainEvents();
        }

        if (creditAssessmentSnapshot is not null)
        {
            application.RecordCreditAssessment(creditAssessmentSnapshot, creditAssessmentSnapshot.AssessedAt);
            application.ClearDomainEvents();
        }

        if (IsFinalStatus(status) && creditProfileSnapshot is not null)
        {
            application.CompleteDecision(creditProfileSnapshot, GetDateTimeOffset(item, "UpdatedAt"));
            application.ClearDomainEvents();
        }

        return application;
    }

    private static ClientProfile ToClientProfile(IReadOnlyDictionary<string, AttributeValue> item)
    {
        return ClientProfile.Create(
            GetString(item, "ClientId"),
            GetString(item, "TaxId"),
            GetString(item, "LegalName"),
            GetString(item, "Industry"),
            GetDateTimeOffset(item, "CreatedAt"));
    }

    private static CreditAssessment ToCreditAssessment(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var assessment = new CreditAssessment(
            GetString(item, "AssessmentId"),
            GetString(item, "ClientId"),
            GetString(item, "ApplicationId"),
            GetEnum<CreditAssessmentResult>(item, "Result"),
            GetInt(item, "Score"),
            GetJson<string[]>(item, "Reasons") ?? [],
            GetJson<CreditProductOffer[]>(item, "EligibleProducts") ?? [],
            GetDateTimeOffset(item, "AssessedAt"));

        assessment.ClearDomainEvents();

        return assessment;
    }

    private static CreditProfile ToCreditProfile(IReadOnlyDictionary<string, AttributeValue> item)
    {
        return CreditProfile.Create(GetString(item, "ClientId"));
    }

    private static DomainEventRecord ToDomainEventRecord(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var eventVersion = item.TryGetValue("EventVersion", out var eventVersionAttribute) &&
            int.TryParse(eventVersionAttribute.N, CultureInfo.InvariantCulture, out var parsedEventVersion)
                ? parsedEventVersion
                : 1;

        var source = item.TryGetValue("Source", out var sourceAttribute) &&
            !string.IsNullOrWhiteSpace(sourceAttribute.S)
                ? sourceAttribute.S
                : "CreditFlow.Api";

        object data = item.TryGetValue("Data", out var dataAttribute) &&
            !string.IsNullOrWhiteSpace(dataAttribute.S)
                ? JsonSerializer.Deserialize<object>(dataAttribute.S, JsonOptions) ?? dataAttribute.S
                : new { };

        return new DomainEventRecord(
            GetString(item, "ApplicationId"),
            Guid.Parse(GetString(item, "EventId")),
            GetString(item, "EventType"),
            eventVersion,
            GetString(item, "CorrelationId"),
            GetDateTimeOffset(item, "OccurredAt"),
            source,
            data);
    }

    private static bool IsFinalStatus(ApplicationStatus status)
    {
        return status is ApplicationStatus.Approved
            or ApplicationStatus.ManualReview
            or ApplicationStatus.Rejected;
    }

    private static void AddJson<T>(Dictionary<string, AttributeValue> item, string name, T? value)
    {
        if (value is null)
        {
            return;
        }

        item[name] = S(JsonSerializer.Serialize(value, JsonOptions));
    }

    private static T? GetJson<T>(IReadOnlyDictionary<string, AttributeValue> item, string name)
    {
        if (!item.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value.S))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(value.S, JsonOptions);
    }

    private static string GetString(IReadOnlyDictionary<string, AttributeValue> item, string name)
    {
        if (!item.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value.S))
        {
            throw new InvalidOperationException($"DynamoDB item is missing required string attribute '{name}'.");
        }

        return value.S;
    }

    private static int GetInt(IReadOnlyDictionary<string, AttributeValue> item, string name)
    {
        return int.Parse(GetNumber(item, name), CultureInfo.InvariantCulture);
    }

    private static decimal GetDecimal(IReadOnlyDictionary<string, AttributeValue> item, string name)
    {
        return decimal.Parse(GetNumber(item, name), CultureInfo.InvariantCulture);
    }

    private static TEnum GetEnum<TEnum>(IReadOnlyDictionary<string, AttributeValue> item, string name)
        where TEnum : struct
    {
        return Enum.Parse<TEnum>(GetString(item, name), ignoreCase: false);
    }

    private static DateTimeOffset GetDateTimeOffset(IReadOnlyDictionary<string, AttributeValue> item, string name)
    {
        return DateTimeOffset.Parse(
            GetString(item, name),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
    }

    private static string GetNumber(IReadOnlyDictionary<string, AttributeValue> item, string name)
    {
        if (!item.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value.N))
        {
            throw new InvalidOperationException($"DynamoDB item is missing required number attribute '{name}'.");
        }

        return value.N;
    }

    private static AttributeValue S(string value) => new()
    {
        S = value
    };

    private static AttributeValue S(Guid value) => new()
    {
        S = value.ToString("D")
    };

    private static AttributeValue S(DateTimeOffset value) => new()
    {
        S = value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
    };

    private static AttributeValue N(int value) => new()
    {
        N = value.ToString(CultureInfo.InvariantCulture)
    };

    private static AttributeValue N(decimal value) => new()
    {
        N = value.ToString(CultureInfo.InvariantCulture)
    };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
