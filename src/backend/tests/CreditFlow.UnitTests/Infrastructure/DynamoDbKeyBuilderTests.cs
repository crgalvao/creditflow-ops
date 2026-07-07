using CreditFlow.Infrastructure.Persistence.DynamoDb;

namespace CreditFlow.UnitTests.Infrastructure;

public sealed class DynamoDbKeyBuilderTests
{
    [Fact]
    public void ApplicationKeys_AreStableAndReadable()
    {
        Assert.Equal("APP#app-001", DynamoDbKeyBuilder.ApplicationPartitionKey("app-001"));
        Assert.Equal("METADATA", DynamoDbKeyBuilder.ApplicationMetadataSortKey());
    }

    [Fact]
    public void UserListingKeys_UseExpectedPrefixes()
    {
        var createdAt = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("USER#user-001", DynamoDbKeyBuilder.UserApplicationsPartitionKey("user-001"));

        var sortKey = DynamoDbKeyBuilder.UserApplicationsSortKey(createdAt, "app-001");

        Assert.StartsWith("CREATED#2026-07-03T12:00:00", sortKey, StringComparison.Ordinal);
        Assert.EndsWith("#APP#app-001", sortKey, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusListingKeys_UseExpectedPrefixes()
    {
        var updatedAt = new DateTimeOffset(2026, 7, 3, 12, 10, 0, TimeSpan.Zero);

        Assert.Equal("STATUS#Approved", DynamoDbKeyBuilder.StatusApplicationsPartitionKey("Approved"));

        var sortKey = DynamoDbKeyBuilder.StatusApplicationsSortKey(updatedAt, "app-001");

        Assert.StartsWith("UPDATED#2026-07-03T12:10:00", sortKey, StringComparison.Ordinal);
        Assert.EndsWith("#APP#app-001", sortKey, StringComparison.Ordinal);
    }

    [Fact]
    public void EventSortKey_OrdersByTimestampBeforeEventId()
    {
        var occurredAt = new DateTimeOffset(2026, 7, 3, 12, 20, 0, TimeSpan.Zero);
        var eventId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var sortKey = DynamoDbKeyBuilder.ApplicationEventSortKey(occurredAt, eventId);

        Assert.StartsWith("EVENT#2026-07-03T12:20:00", sortKey, StringComparison.Ordinal);
        Assert.EndsWith("#11111111111111111111111111111111", sortKey, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyKeyValues_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => DynamoDbKeyBuilder.ApplicationPartitionKey(""));
        Assert.Throws<ArgumentException>(() => DynamoDbKeyBuilder.ClientPartitionKey(" "));
        Assert.Throws<ArgumentException>(() => DynamoDbKeyBuilder.TaxIdPartitionKey(""));
    }
}
