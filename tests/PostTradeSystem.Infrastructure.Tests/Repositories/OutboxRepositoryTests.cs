using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Entities;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Tests.Integration;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Repositories;

[Collection("SqlServer")]
public class OutboxRepositoryTests : SqlServerTestBase
{
    private readonly OutboxRepository _repository;
    private readonly PostTradeDbContext _context;

    public OutboxRepositoryTests(SqlServerFixture fixture) : base(fixture)
    {
        _context = Context;
        _repository = new OutboxRepository(_context);
    }

    [Fact]
    public async Task SaveOutboxEventAsync_ShouldSaveEventToDatabase()
    {
        // Arrange
        var outboxEvent = new OutboxEventEntity
        {
            EventId = "event-123",
            AggregateId = "TRADE-001",
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{\"tradeId\":\"TRADE-001\"}",
            Metadata = "{\"version\":\"1.0\"}",
            Topic = "events.trades",
            PartitionKey = "partition-key",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            RetryCount = 0
        };

        // Act
        await _repository.SaveOutboxEventAsync(outboxEvent);

        // Assert
        var savedEvent = await _context.OutboxEvents
            .FirstOrDefaultAsync(e => e.EventId == "event-123");

        savedEvent.Should().NotBeNull();
        savedEvent!.AggregateId.Should().Be("TRADE-001");
        savedEvent.EventType.Should().Be("TradeCreated");
        savedEvent.Topic.Should().Be("events.trades");
        savedEvent.IsProcessed.Should().BeFalse();
    }

    [Fact]
    public async Task GetUnprocessedEventsAsync_ShouldReturnUnprocessedEvents()
    {
        // Arrange
        var processedEvent = new OutboxEventEntity
        {
            EventId = "processed-event",
            AggregateId = "TRADE-001",
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = "{}",
            Topic = "events.trades",
            PartitionKey = "key1",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            IsProcessed = true
        };

        var unprocessedEvent = new OutboxEventEntity
        {
            EventId = "unprocessed-event",
            AggregateId = "TRADE-002",
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = "{}",
            Topic = "events.trades",
            PartitionKey = "key2",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            IsProcessed = false
        };

        _context.OutboxEvents.AddRange(processedEvent, unprocessedEvent);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUnprocessedEventsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.First().EventId.Should().Be("unprocessed-event");
        result.First().IsProcessed.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAsProcessedAsync_ShouldUpdateEventStatus()
    {
        // Arrange
        var outboxEvent = new OutboxEventEntity
        {
            EventId = "event-to-process",
            AggregateId = "TRADE-001",
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = "{}",
            Topic = "events.trades",
            PartitionKey = "key1",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false
        };

        _context.OutboxEvents.Add(outboxEvent);
        await _context.SaveChangesAsync();

        // Act
        await _repository.MarkAsProcessedAsync(outboxEvent.Id);

        // Assert
        var updatedEvent = await _context.OutboxEvents
            .FirstOrDefaultAsync(e => e.Id == outboxEvent.Id);

        updatedEvent.Should().NotBeNull();
        updatedEvent!.IsProcessed.Should().BeTrue();
        updatedEvent.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetFailedEventsForRetryAsync_ShouldReturnEligibleEvents()
    {
        // Arrange
        var retryDelay = TimeSpan.FromMinutes(5);
        var maxRetryCount = 3;

        var eligibleEvent = new OutboxEventEntity
        {
            EventId = "eligible-event",
            AggregateId = "TRADE-001",
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = "{}",
            Topic = "events.trades",
            PartitionKey = "key1",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            IsProcessed = false,
            RetryCount = 1,
            LastRetryAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var tooRecentEvent = new OutboxEventEntity
        {
            EventId = "too-recent-event",
            AggregateId = "TRADE-002",
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = "{}",
            Topic = "events.trades",
            PartitionKey = "key2",
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            IsProcessed = false,
            RetryCount = 1,
            LastRetryAt = DateTime.UtcNow.AddMinutes(-2)
        };

        var maxRetriesEvent = new OutboxEventEntity
        {
            EventId = "max-retries-event",
            AggregateId = "TRADE-003",
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = "{}",
            Topic = "events.trades",
            PartitionKey = "key3",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            IsProcessed = false,
            RetryCount = 3,
            LastRetryAt = DateTime.UtcNow.AddMinutes(-10)
        };

        _context.OutboxEvents.AddRange(eligibleEvent, tooRecentEvent, maxRetriesEvent);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetFailedEventsForRetryAsync(retryDelay, maxRetryCount);

        // Assert
        result.Should().HaveCount(1);
        result.First().EventId.Should().Be("eligible-event");
    }

    [Fact]
    public async Task IncrementRetryCountAsync_ShouldUpdateRetryCount()
    {
        // Arrange
        var outboxEvent = new OutboxEventEntity
        {
            EventId = "retry-event",
            AggregateId = "TRADE-001",
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = "{}",
            Topic = "events.trades",
            PartitionKey = "key1",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            RetryCount = 1
        };

        _context.OutboxEvents.Add(outboxEvent);
        await _context.SaveChangesAsync();

        // Act
        await _repository.IncrementRetryCountAsync(outboxEvent.Id);

        // Assert
        var updatedEvent = await _context.OutboxEvents
            .FirstOrDefaultAsync(e => e.Id == outboxEvent.Id);

        updatedEvent.Should().NotBeNull();
        updatedEvent!.RetryCount.Should().Be(2);
        updatedEvent.LastRetryAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}