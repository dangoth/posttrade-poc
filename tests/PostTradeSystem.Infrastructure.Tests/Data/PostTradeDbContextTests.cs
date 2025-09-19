using Microsoft.EntityFrameworkCore;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Entities;
using PostTradeSystem.Infrastructure.Tests.Integration;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Data;

public class PostTradeDbContextTests : SqlServerTestBase
{
    public PostTradeDbContextTests(SqlServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task SaveChangesAsync_UpdatesProjectionTimestamps()
    {
        
        var projection = new ProjectionEntity
        {
            ProjectionName = "TestProjection",
            AggregateId = Guid.NewGuid().ToString(),
            AggregateType = "Trade",
            PartitionKey = "TRADER001:AAPL",
            LastProcessedVersion = 1,
            ProjectionData = "{\"test\": \"data\"}",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        };

        Context.Projections.Add(projection);
        await Context.SaveChangesAsync();

        var originalUpdatedAt = projection.UpdatedAt;
        
        await Task.Delay(10);
        
        projection.ProjectionData = "{\"test\": \"updated\"}";
        Context.Projections.Update(projection);
        await Context.SaveChangesAsync();

        Assert.True(projection.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task DbContext_CanCreateAllEntities()
    {

        var eventEntity = new EventStoreEntity
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = Guid.NewGuid().ToString(),
            AggregateType = "Trade",
            PartitionKey = "TRADER001:AAPL",
            AggregateVersion = 1,
            EventType = "TradeCreatedEvent",
            EventData = "{\"test\": \"data\"}",
            Metadata = "{\"version\": \"1.0\"}",
            OccurredAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString(),
            CausedBy = "TestSystem"
        };

        var snapshotEntity = new SnapshotEntity
        {
            AggregateId = Guid.NewGuid().ToString(),
            AggregateType = "Trade",
            PartitionKey = "TRADER001:AAPL",
            AggregateVersion = 5,
            SnapshotData = "{\"state\": \"data\"}",
            Metadata = "{\"version\": \"1.0\"}"
        };

        var projectionEntity = new ProjectionEntity
        {
            ProjectionName = "TradesByTrader",
            AggregateId = Guid.NewGuid().ToString(),
            AggregateType = "Trade",
            PartitionKey = "TRADER001:AAPL",
            LastProcessedVersion = 3,
            ProjectionData = "{\"projection\": \"data\"}"
        };

        var idempotencyEntity = new IdempotencyEntity
        {
            IdempotencyKey = "KEY001",
            AggregateId = Guid.NewGuid().ToString(),
            RequestHash = "HASH001",
            ResponseData = "{\"response\": \"data\"}",
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        Context.EventStore.Add(eventEntity);
        Context.Snapshots.Add(snapshotEntity);
        Context.Projections.Add(projectionEntity);
        Context.IdempotencyKeys.Add(idempotencyEntity);

        await Context.SaveChangesAsync();

        Assert.Equal(1, await Context.EventStore.CountAsync());
        Assert.Equal(1, await Context.Snapshots.CountAsync());
        Assert.Equal(1, await Context.Projections.CountAsync());
        Assert.Equal(1, await Context.IdempotencyKeys.CountAsync());
    }

    [Fact]
    public async Task EventStore_EnforcesUniqueEventId()
    {

        var eventId = Guid.NewGuid().ToString();
        
        var event1 = new EventStoreEntity
        {
            EventId = eventId,
            AggregateId = Guid.NewGuid().ToString(),
            AggregateType = "Trade",
            PartitionKey = "TRADER001:AAPL",
            AggregateVersion = 1,
            EventType = "TradeCreatedEvent",
            EventData = "{\"test\": \"data1\"}",
            OccurredAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString(),
            CausedBy = "TestSystem"
        };

        var event2 = new EventStoreEntity
        {
            EventId = eventId,
            AggregateId = Guid.NewGuid().ToString(),
            AggregateType = "Trade",
            PartitionKey = "TRADER002:AAPL",
            AggregateVersion = 1,
            EventType = "TradeCreatedEvent",
            EventData = "{\"test\": \"data2\"}",
            OccurredAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString(),
            CausedBy = "TestSystem"
        };

        Context.EventStore.Add(event1);
        await Context.SaveChangesAsync();

        Context.EventStore.Add(event2);
        
        await Assert.ThrowsAsync<DbUpdateException>(
            async () => await Context.SaveChangesAsync());
    }

    [Fact]
    public async Task EventStore_EnforcesUniqueAggregateVersion()
    {

        var aggregateId = Guid.NewGuid().ToString();
        
        var event1 = new EventStoreEntity
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = aggregateId,
            AggregateType = "Trade",
            PartitionKey = "TRADER001:AAPL",
            AggregateVersion = 1,
            EventType = "TradeCreatedEvent",
            EventData = "{\"test\": \"data1\"}",
            OccurredAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString(),
            CausedBy = "TestSystem"
        };

        var event2 = new EventStoreEntity
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = aggregateId,
            AggregateType = "Trade",
            PartitionKey = "TRADER001:AAPL",
            AggregateVersion = 1,
            EventType = "TradeStatusChangedEvent",
            EventData = "{\"test\": \"data2\"}",
            OccurredAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString(),
            CausedBy = "TestSystem"
        };

        Context.EventStore.Add(event1);
        await Context.SaveChangesAsync();

        Context.EventStore.Add(event2);
        
        await Assert.ThrowsAsync<DbUpdateException>(
            async () => await Context.SaveChangesAsync());
    }
}