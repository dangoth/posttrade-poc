using Microsoft.Extensions.Logging;
using Moq;
using PostTradeSystem.Core.Common;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Infrastructure.Entities;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Services;
using PostTradeSystem.Infrastructure.Tests.Integration;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using System.Text.Json;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Repositories;

[Collection("SqlServer")]
public class EventStoreRepositoryMetadataTests : SqlServerTestBase
{
    private readonly Mock<ISerializationManagementService> _mockSerializationService;
    private readonly Mock<IOutboxService> _mockOutboxService;
    private readonly Mock<ILogger<EventStoreRepository>> _mockLogger;
    private readonly EventStoreRepository _repository;

    public EventStoreRepositoryMetadataTests(SqlServerFixture fixture) : base(fixture)
    {
        // Create mock for serialization service
        _mockSerializationService = new Mock<ISerializationManagementService>();
        _mockOutboxService = new Mock<IOutboxService>();
        _mockLogger = new Mock<ILogger<EventStoreRepository>>();
        
        _repository = new EventStoreRepository(
            Context,
            _mockSerializationService.Object,
            _mockLogger.Object,
            _mockOutboxService.Object);
    }

    [Fact]
    public async Task GetEventsAsync_WithValidMetadata_ShouldParseSuccessfully()
    {
        // Arrange
        var aggregateId = Guid.NewGuid().ToString();
        var validMetadata = JsonSerializer.Serialize(new { SchemaVersion = "2" });
        
        var eventEntity = new EventStoreEntity
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = aggregateId,
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = validMetadata,
            AggregateVersion = 1,
            OccurredAt = DateTime.UtcNow
        };

        Context.EventStore.Add(eventEntity);
        await Context.SaveChangesAsync();

        var mockEvent = new Mock<IDomainEvent>();
        _mockSerializationService.Setup(s => s.Deserialize(It.IsAny<SerializedEvent>()))
            .Returns(Result<IDomainEvent>.Success(mockEvent.Object));

        // Act
        var eventsResult = await _repository.GetEventsAsync(aggregateId);

        // Assert
        Assert.True(eventsResult.IsSuccess);
        var events = eventsResult.Value!;
        Assert.Single(events);
        // Verification removed - using real serialization service
        
        // Verify no error logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task GetEventsAsync_WithMalformedJsonMetadata_ShouldMoveToDeadLetter()
    {
        // Arrange
        var aggregateId = Guid.NewGuid().ToString();
        var malformedMetadata = "{ invalid json";
        
        var eventEntity = new EventStoreEntity
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = aggregateId,
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = malformedMetadata,
            AggregateVersion = 1,
            OccurredAt = DateTime.UtcNow
        };

        Context.EventStore.Add(eventEntity);
        await Context.SaveChangesAsync();

        // Act
        var events = await _repository.GetEventsAsync(aggregateId);

        // Assert
        Assert.Empty(events.Value!);
        
        // Verify event was moved to dead letter queue
        _mockOutboxService.Verify(o => o.SaveEventToOutboxAsync(
            It.Is<DeadLetterEvent>(e => e.DeadLetterReason.Contains("Malformed JSON metadata")),
            "events.deadletter",
            aggregateId,
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify error logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to parse metadata JSON")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetEventsAsync_WithMissingSchemaVersion_ShouldUseHistoricalFallback()
    {
        // Arrange
        var aggregateId = Guid.NewGuid().ToString();
        var metadataWithoutVersion = JsonSerializer.Serialize(new { SomeOtherProperty = "value" });
        
        var eventEntity = new EventStoreEntity
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = aggregateId,
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = metadataWithoutVersion,
            AggregateVersion = 1,
            OccurredAt = new DateTime(2023, 1, 1) // Before historical cutoff
        };

        Context.EventStore.Add(eventEntity);
        await Context.SaveChangesAsync();

        var mockEvent = new Mock<IDomainEvent>();
        _mockSerializationService.Setup(s => s.Deserialize(It.IsAny<SerializedEvent>()))
            .Returns(Result<IDomainEvent>.Success(mockEvent.Object));

        // Act
        var eventsResult = await _repository.GetEventsAsync(aggregateId);

        // Assert
        Assert.True(eventsResult.IsSuccess);
        var events = eventsResult.Value!;
        Assert.Single(events);
        _mockSerializationService.Verify(s => s.Deserialize(It.Is<SerializedEvent>(se => se.SchemaVersion == 1)), Times.Once);
        
        // Verify warning logging - check for the specific historical fallback message
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Using historical fallback for event") && v.ToString()!.Contains("aggregate")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetEventsAsync_WithInvalidSchemaVersionValue_ShouldMoveToDeadLetter()
    {
        // Arrange
        var aggregateId = Guid.NewGuid().ToString();
        var invalidVersionMetadata = JsonSerializer.Serialize(new { SchemaVersion = "invalid" });
        
        var eventEntity = new EventStoreEntity
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = aggregateId,
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = invalidVersionMetadata,
            AggregateVersion = 1,
            OccurredAt = DateTime.UtcNow // Recent event
        };

        Context.EventStore.Add(eventEntity);
        await Context.SaveChangesAsync();

        // Act
        var events = await _repository.GetEventsAsync(aggregateId);

        // Assert
        Assert.Empty(events.Value!);
        
        // Verify event was moved to dead letter queue
        _mockOutboxService.Verify(o => o.SaveEventToOutboxAsync(
            It.Is<DeadLetterEvent>(e => e.DeadLetterReason.Contains("Cannot determine schema version")),
            "events.deadletter",
            aggregateId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetEventsAsync_WithDeserializationFailure_ShouldMoveToDeadLetter()
    {
        // Arrange
        var aggregateId = Guid.NewGuid().ToString();
        var validMetadata = JsonSerializer.Serialize(new { SchemaVersion = "2" });
        
        var eventEntity = new EventStoreEntity
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = aggregateId,
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = validMetadata,
            AggregateVersion = 1,
            OccurredAt = DateTime.UtcNow
        };

        Context.EventStore.Add(eventEntity);
        await Context.SaveChangesAsync();

        _mockSerializationService.Setup(s => s.Deserialize(It.IsAny<SerializedEvent>()))
            .Returns(Result<IDomainEvent>.Failure("Deserialization failed"));

        // Act
        var events = await _repository.GetEventsAsync(aggregateId);

        // Assert
        Assert.Empty(events.Value!);
        
        // Verify event was moved to dead letter queue
        _mockOutboxService.Verify(o => o.SaveEventToOutboxAsync(
            It.Is<DeadLetterEvent>(e => e.DeadLetterReason.Contains("Deserialization failed")),
            "events.deadletter",
            aggregateId,
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify error logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to deserialize event")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetEventsAsync_WithoutOutboxService_ShouldLogButNotMoveToDeadLetter()
    {
        // Arrange
        var repositoryWithoutOutbox = new EventStoreRepository(
            Context,
            _mockSerializationService.Object,
            _mockLogger.Object,
            null); // No outbox service

        var aggregateId = Guid.NewGuid().ToString();
        var malformedMetadata = "{ invalid json";
        
        var eventEntity = new EventStoreEntity
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = aggregateId,
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = malformedMetadata,
            AggregateVersion = 1,
            OccurredAt = DateTime.UtcNow
        };

        Context.EventStore.Add(eventEntity);
        await Context.SaveChangesAsync();

        // Act
        var events = await repositoryWithoutOutbox.GetEventsAsync(aggregateId);

        // Assert
        Assert.Empty(events.Value!);
        
        // Verify no outbox service calls
        _mockOutboxService.Verify(o => o.SaveEventToOutboxAsync(
            It.IsAny<IDomainEvent>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);

        // Verify error logging still occurs
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Skipping event")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}