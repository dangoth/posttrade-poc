using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Infrastructure.Kafka;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Schemas;
using PostTradeSystem.Infrastructure.Services;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Integration;

[Collection("SqlServer")]
public class TransactionalOutboxIntegrationTests : IntegrationTestBase
{
    public TransactionalOutboxIntegrationTests(SqlServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task SaveEventsAsync_ShouldSaveToEventStoreAndOutboxInSameTransaction()
    {
        // Arrange
        var tradeEvent = new TradeCreatedEvent(
            "TRADE-001",
            "TRADER-001",
            "INST-001",
            100m,
            50.25m,
            "BUY",
            DateTime.UtcNow,
            "USD",
            "COUNTERPARTY-001",
            "EQUITY",
            1,
            "correlation-123",
            "IntegrationTest",
            new Dictionary<string, object>
            {
                ["Symbol"] = "AAPL",
                ["Exchange"] = "NASDAQ"
            });

        var partitionKey = "TRADER-001:INST-001";

        // Act
        await EventStoreRepository.SaveEventsAsync(
            tradeEvent.AggregateId,
            partitionKey,
            new[] { tradeEvent },
            0);

        // Assert - Event should be saved to event store
        var savedEvents = await EventStoreRepository.GetEventsAsync(tradeEvent.AggregateId);
        savedEvents.Value.Should().HaveCount(1);
        
        var savedEvent = savedEvents.Value!.First() as TradeCreatedEvent;
        savedEvent.Should().NotBeNull();
        savedEvent!.AggregateId.Should().Be("TRADE-001");
        savedEvent.TraderId.Should().Be("TRADER-001");
        savedEvent.InstrumentId.Should().Be("INST-001");

        // Assert - Event should also be saved to outbox
        var context = Context;
        var outboxEvents = await context.OutboxEvents
            .Where(e => e.EventId == tradeEvent.EventId)
            .ToListAsync();
            
        var allOutboxEvents = await context.OutboxEvents.ToListAsync();

        outboxEvents.Should().HaveCount(1);
        var outboxEvent = outboxEvents.First();
        outboxEvent.AggregateId.Should().Be("TRADE-001");
        outboxEvent.EventType.Should().Be("TradeCreated");
        outboxEvent.Topic.Should().Be("events.trades");
        outboxEvent.PartitionKey.Should().Be(partitionKey);
        outboxEvent.IsProcessed.Should().BeFalse();
    }

    [Fact]
    public async Task SaveEventsAsync_WhenOutboxFails_ShouldRollbackEntireTransaction()
    {
        // Arrange
        var mockOutboxService = new Mock<IOutboxService>();
        mockOutboxService
            .Setup(x => x.SaveEventToOutboxAsync(It.IsAny<IDomainEvent>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated outbox failure"));

        var failingEventStoreRepository = new EventStoreRepository(
            Context,
            SerializationService,
            Mock.Of<ILogger<EventStoreRepository>>(),
            mockOutboxService.Object);

        var tradeEvent = new TradeCreatedEvent(
            "TRADE-002",
            "TRADER-002",
            "INST-002",
            200m,
            75.50m,
            "SELL",
            DateTime.UtcNow,
            "EUR",
            "COUNTERPARTY-002",
            "EQUITY",
            1,
            "correlation-456",
            "IntegrationTest",
            new Dictionary<string, object>());

        var partitionKey = "TRADER-002:INST-002";

        // Act & Assert - Should return failure result and rollback transaction
        var result = await failingEventStoreRepository.SaveEventsAsync(
            tradeEvent.AggregateId,
            partitionKey,
            new[] { tradeEvent },
            0);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Simulated outbox failure");

        // Assert - Both event store and outbox should be empty
        var context = Context;
        
        var eventStoreCount = await context.EventStore
            .CountAsync(e => e.EventId == tradeEvent.EventId);
        
        var outboxCount = await context.OutboxEvents
            .CountAsync(e => e.EventId == tradeEvent.EventId);

        eventStoreCount.Should().Be(0);
        outboxCount.Should().Be(0);
        
        mockOutboxService.Verify(x => x.SaveEventToOutboxAsync(
            It.Is<IDomainEvent>(e => e.EventId == tradeEvent.EventId),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveEventsAsync_ShouldHandleIdempotency()
    {
        // Arrange
        var tradeEvent = new TradeCreatedEvent(
            "TRADE-003",
            "TRADER-003",
            "INST-003",
            150m,
            25.75m,
            "BUY",
            DateTime.UtcNow,
            "GBP",
            "COUNTERPARTY-003",
            "EQUITY",
            1,
            "correlation-789",
            "IntegrationTest",
            new Dictionary<string, object>());

        var partitionKey = "TRADER-003:INST-003";

        // Act
        await EventStoreRepository.SaveEventsAsync(
            tradeEvent.AggregateId,
            partitionKey,
            new[] { tradeEvent },
            0);

        await EventStoreRepository.SaveEventsAsync(
            tradeEvent.AggregateId,
            partitionKey,
            new[] { tradeEvent },
            1);

        // Assert - Should only have one event in both stores
        var savedEvents = await EventStoreRepository.GetEventsAsync(tradeEvent.AggregateId);
        savedEvents.Value.Should().HaveCount(1);

        var context = Context;
        var outboxEvents = await context.OutboxEvents
            .Where(e => e.EventId == tradeEvent.EventId)
            .ToListAsync();

        outboxEvents.Should().HaveCount(1);
    }

    [Fact]
    public async Task EventSerialization_ShouldBeValidAgainstSchema()
    {
        // Arrange
        var tradeEvent = new TradeCreatedEvent(
            "TRADE-004",
            "TRADER-004",
            "INST-004",
            300m,
            100.00m,
            "BUY",
            DateTime.UtcNow,
            "USD",
            "COUNTERPARTY-004",
            "EQUITY",
            1,
            "correlation-abc",
            "SchemaTest",
            new Dictionary<string, object>
            {
                ["Symbol"] = "MSFT",
                ["Exchange"] = "NASDAQ",
                ["Sector"] = "Technology"
            });

        // Act
        var serializedEvent = await SerializationService.SerializeAsync(tradeEvent);

        // Assert
        serializedEvent.IsSuccess.Should().BeTrue();
        serializedEvent.Value!.Data.Should().NotBeNullOrEmpty();
        serializedEvent.Value.SchemaVersion.Should().Be(2);

        var isValid = SchemaValidator.ValidateMessage("TradeCreatedEvent", serializedEvent.Value.Data, serializedEvent.Value.SchemaVersion);
        isValid.Should().BeTrue();

        var deserializeResult = SerializationService.Deserialize(serializedEvent.Value);
        deserializeResult.IsSuccess.Should().BeTrue();
        var deserializedEvent = deserializeResult.Value!;
        deserializedEvent.Should().BeOfType<TradeCreatedEvent>();
        
        var deserializedTradeEvent = (TradeCreatedEvent)deserializedEvent;
        deserializedTradeEvent.AggregateId.Should().Be("TRADE-004");
        deserializedTradeEvent.TraderId.Should().Be("TRADER-004");
        deserializedTradeEvent.AdditionalData.Should().ContainKey("Symbol");
        
        var symbolValue = deserializedTradeEvent.AdditionalData["Symbol"];
        ((System.Text.Json.JsonElement)symbolValue).GetString().Should().Be("MSFT");
    }

    [Fact]
    public async Task OutboxEvent_ShouldContainValidSerializedData()
    {
        // Arrange
        var tradeEvent = new TradeCreatedEvent(
            "TRADE-005",
            "TRADER-005",
            "INST-005",
            500m,
            200.25m,
            "SELL",
            DateTime.UtcNow,
            "JPY",
            "COUNTERPARTY-005",
            "FX",
            1,
            "correlation-def",
            "SerializationTest",
            new Dictionary<string, object>
            {
                ["BaseCurrency"] = "USD",
                ["QuoteCurrency"] = "JPY",
                ["SpotRate"] = 150.25m
            });

        var partitionKey = "TRADER-005:INST-005";

        // Act
        await EventStoreRepository.SaveEventsAsync(
            tradeEvent.AggregateId,
            partitionKey,
            new[] { tradeEvent },
            0);

        // Assert
        var context = Context;
        var outboxEvent = await context.OutboxEvents
            .FirstOrDefaultAsync(e => e.EventId == tradeEvent.EventId);

        outboxEvent.Should().NotBeNull();
        outboxEvent!.EventData.Should().NotBeNullOrEmpty();

        var isValidJson = System.Text.Json.JsonDocument.Parse(outboxEvent.EventData);
        isValidJson.Should().NotBeNull();

        var metadata = System.Text.Json.JsonDocument.Parse(outboxEvent!.Metadata);
        var schemaVersionStr = metadata.RootElement.GetProperty("SchemaVersion").GetString();
        var schemaVersion = int.Parse(schemaVersionStr!);
        
        var serializedEvent = new Core.Serialization.SerializedEvent(
            outboxEvent.EventType,
            schemaVersion,
            outboxEvent.EventData,
            "default",
            DateTime.UtcNow,
            new Dictionary<string, string>());

        var deserializeResult = SerializationService.Deserialize(serializedEvent);
        deserializeResult.IsSuccess.Should().BeTrue();
        var deserializedEvent = deserializeResult.Value!;
        deserializedEvent.Should().BeOfType<TradeCreatedEvent>();
    }
}