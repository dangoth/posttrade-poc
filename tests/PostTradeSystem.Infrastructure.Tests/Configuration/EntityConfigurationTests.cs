using Microsoft.EntityFrameworkCore;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Entities;
using PostTradeSystem.Infrastructure.Tests.Integration;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using FluentAssertions;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Configuration;

public class EntityConfigurationTests : SqlServerTestBase
{
    public EntityConfigurationTests(SqlServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void EventStoreEntity_HasCorrectConfiguration()
    {
        
        var entityType = Context.Model.FindEntityType(typeof(EventStoreEntity));

        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("EventStore");

        var eventIdProperty = entityType!.FindProperty(nameof(EventStoreEntity.EventId));
        eventIdProperty.Should().NotBeNull();
        eventIdProperty!.IsNullable.Should().BeFalse();
        eventIdProperty.GetMaxLength().Should().Be(50);

        var aggregateIdProperty = entityType.FindProperty(nameof(EventStoreEntity.AggregateId));
        aggregateIdProperty.Should().NotBeNull();
        aggregateIdProperty!.IsNullable.Should().BeFalse();
        aggregateIdProperty.GetMaxLength().Should().Be(100);

        var eventDataProperty = entityType.FindProperty(nameof(EventStoreEntity.EventData));
        eventDataProperty.Should().NotBeNull();
        eventDataProperty!.IsNullable.Should().BeFalse();
        eventDataProperty.GetColumnType().Should().Be("nvarchar(max)");

        var isProcessedProperty = entityType.FindProperty(nameof(EventStoreEntity.IsProcessed));
        isProcessedProperty.Should().NotBeNull();
        isProcessedProperty!.GetDefaultValue().Should().Be(false);
    }

    [Fact]
    public void EventStoreEntity_HasCorrectIndexes()
    {
        
        var entityType = Context.Model.FindEntityType(typeof(EventStoreEntity));

        var indexes = entityType!.GetIndexes().ToList();
        indexes.Should().HaveCount(5);

        var eventIdIndex = indexes.FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(EventStoreEntity.EventId)));
        eventIdIndex.Should().NotBeNull();
        eventIdIndex!.IsUnique.Should().BeTrue();
        eventIdIndex.GetDatabaseName().Should().Be("IX_EventStore_EventId");

        var aggregateVersionIndex = indexes.FirstOrDefault(i => 
            i.Properties.Count == 2 && 
            i.Properties.Any(p => p.Name == nameof(EventStoreEntity.AggregateId)) &&
            i.Properties.Any(p => p.Name == nameof(EventStoreEntity.AggregateVersion)));
        aggregateVersionIndex.Should().NotBeNull();
        aggregateVersionIndex!.IsUnique.Should().BeTrue();
        aggregateVersionIndex.GetDatabaseName().Should().Be("IX_EventStore_AggregateId_Version");

        var partitionKeyIndex = indexes.FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(EventStoreEntity.PartitionKey)));
        partitionKeyIndex.Should().NotBeNull();
        partitionKeyIndex!.GetDatabaseName().Should().Be("IX_EventStore_PartitionKey");
    }

    [Fact]
    public void SnapshotEntity_HasCorrectConfiguration()
    {
        
        var entityType = Context.Model.FindEntityType(typeof(SnapshotEntity));

        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("Snapshots");

        var aggregateIdProperty = entityType!.FindProperty(nameof(SnapshotEntity.AggregateId));
        aggregateIdProperty.Should().NotBeNull();
        aggregateIdProperty!.IsNullable.Should().BeFalse();
        aggregateIdProperty.GetMaxLength().Should().Be(100);

        var snapshotDataProperty = entityType.FindProperty(nameof(SnapshotEntity.SnapshotData));
        snapshotDataProperty.Should().NotBeNull();
        snapshotDataProperty!.IsNullable.Should().BeFalse();
        snapshotDataProperty.GetColumnType().Should().Be("nvarchar(max)");

        var createdAtProperty = entityType.FindProperty(nameof(SnapshotEntity.CreatedAt));
        createdAtProperty.Should().NotBeNull();
        createdAtProperty!.GetDefaultValueSql().Should().Be("GETUTCDATE()");
    }

    [Fact]
    public void ProjectionEntity_HasCorrectConfiguration()
    {
        
        var entityType = Context.Model.FindEntityType(typeof(ProjectionEntity));

        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("Projections");

        var projectionNameProperty = entityType!.FindProperty(nameof(ProjectionEntity.ProjectionName));
        projectionNameProperty.Should().NotBeNull();
        projectionNameProperty!.IsNullable.Should().BeFalse();
        projectionNameProperty.GetMaxLength().Should().Be(100);

        var projectionDataProperty = entityType.FindProperty(nameof(ProjectionEntity.ProjectionData));
        projectionDataProperty.Should().NotBeNull();
        projectionDataProperty!.IsNullable.Should().BeFalse();
        projectionDataProperty.GetColumnType().Should().Be("nvarchar(max)");

        var updatedAtProperty = entityType.FindProperty(nameof(ProjectionEntity.UpdatedAt));
        updatedAtProperty.Should().NotBeNull();
        updatedAtProperty!.GetDefaultValueSql().Should().Be("GETUTCDATE()");
    }

    [Fact]
    public void ProjectionEntity_HasUniqueProjectionNameAggregateIdIndex()
    {
        
        var entityType = Context.Model.FindEntityType(typeof(ProjectionEntity));

        var indexes = entityType!.GetIndexes().ToList();
        
        var uniqueIndex = indexes.FirstOrDefault(i => 
            i.Properties.Count == 2 && 
            i.Properties.Any(p => p.Name == nameof(ProjectionEntity.ProjectionName)) &&
            i.Properties.Any(p => p.Name == nameof(ProjectionEntity.AggregateId)));
            
        uniqueIndex.Should().NotBeNull();
        uniqueIndex!.IsUnique.Should().BeTrue();
        uniqueIndex.GetDatabaseName().Should().Be("IX_Projections_ProjectionName_AggregateId");
    }

    [Fact]
    public void IdempotencyEntity_HasCorrectConfiguration()
    {
        
        var entityType = Context.Model.FindEntityType(typeof(IdempotencyEntity));

        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("IdempotencyKeys");

        var idempotencyKeyProperty = entityType!.FindProperty(nameof(IdempotencyEntity.IdempotencyKey));
        idempotencyKeyProperty.Should().NotBeNull();
        idempotencyKeyProperty!.IsNullable.Should().BeFalse();
        idempotencyKeyProperty.GetMaxLength().Should().Be(100);

        var requestHashProperty = entityType.FindProperty(nameof(IdempotencyEntity.RequestHash));
        requestHashProperty.Should().NotBeNull();
        requestHashProperty!.IsNullable.Should().BeFalse();
        requestHashProperty.GetMaxLength().Should().Be(64);

        var responseDataProperty = entityType.FindProperty(nameof(IdempotencyEntity.ResponseData));
        responseDataProperty.Should().NotBeNull();
        responseDataProperty!.IsNullable.Should().BeFalse();
        responseDataProperty.GetColumnType().Should().Be("nvarchar(max)");
    }

    [Fact]
    public void IdempotencyEntity_HasUniqueIdempotencyKeyIndex()
    {
        
        var entityType = Context.Model.FindEntityType(typeof(IdempotencyEntity));

        var indexes = entityType!.GetIndexes().ToList();
        
        var uniqueIndex = indexes.FirstOrDefault(i => 
            i.Properties.Count == 1 && 
            i.Properties.Any(p => p.Name == nameof(IdempotencyEntity.IdempotencyKey)));
            
        uniqueIndex.Should().NotBeNull();
        uniqueIndex!.IsUnique.Should().BeTrue();
        uniqueIndex.GetDatabaseName().Should().Be("IX_IdempotencyKeys_IdempotencyKey");
    }

    [Fact]
    public async Task AllEntities_CanBeCreatedAndQueried()
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
            SnapshotData = "{\"state\": \"data\"}"
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

        var savedEvent = await Context.EventStore.FirstAsync();
        var savedSnapshot = await Context.Snapshots.FirstAsync();
        var savedProjection = await Context.Projections.FirstAsync();
        var savedIdempotency = await Context.IdempotencyKeys.FirstAsync();

        savedEvent.EventId.Should().Be(eventEntity.EventId);
        savedSnapshot.AggregateId.Should().Be(snapshotEntity.AggregateId);
        savedProjection.ProjectionName.Should().Be(projectionEntity.ProjectionName);
        savedIdempotency.IdempotencyKey.Should().Be(idempotencyEntity.IdempotencyKey);

        savedEvent.IsProcessed.Should().BeFalse();
        savedEvent.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        savedSnapshot.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        savedProjection.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        savedProjection.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        savedIdempotency.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }
}
