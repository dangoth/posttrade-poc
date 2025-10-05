using PostTradeSystem.Infrastructure.Entities;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Tests.Integration;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Repositories;

[Collection("SqlServer")]
public class OutboxRepositoryDlqTests : SqlServerTestBase
{
    private readonly OutboxRepository _repository;

    public OutboxRepositoryDlqTests(SqlServerFixture fixture) : base(fixture)
    {
        _repository = new OutboxRepository(Context);
    }

    [Fact]
    public async Task MoveToDeadLetterAsync_ShouldMarkEventAsDeadLettered()
    {
        // Arrange
        var outboxEvent = new OutboxEventEntity
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = Guid.NewGuid().ToString(),
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = "{}",
            Topic = "trades",
            PartitionKey = "partition1",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            RetryCount = 3
        };

        await _repository.SaveOutboxEventAsync(outboxEvent);

        // Act
        await _repository.MoveToDeadLetterAsync(outboxEvent.Id, "Max retries exceeded");

        // Assert
        var updatedEvent = await Context.OutboxEvents.FindAsync(outboxEvent.Id);
        Assert.NotNull(updatedEvent);
        Assert.True(updatedEvent.IsDeadLettered);
        Assert.NotNull(updatedEvent.DeadLetteredAt);
        Assert.Equal("Max retries exceeded", updatedEvent.DeadLetterReason);
    }

    [Fact]
    public async Task GetDeadLetteredEventsAsync_ShouldReturnOnlyDeadLetteredEvents()
    {
        // Arrange
        var normalEvent = new OutboxEventEntity
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = Guid.NewGuid().ToString(),
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = "{}",
            Topic = "trades",
            PartitionKey = "partition1",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            IsDeadLettered = false
        };

        var deadLetteredEvent = new OutboxEventEntity
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = Guid.NewGuid().ToString(),
            AggregateType = "Trade",
            EventType = "TradeUpdated",
            EventData = "{}",
            Metadata = "{}",
            Topic = "trades",
            PartitionKey = "partition2",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            IsDeadLettered = true,
            DeadLetteredAt = DateTime.UtcNow,
            DeadLetterReason = "Test reason"
        };

        await _repository.SaveOutboxEventAsync(normalEvent);
        await _repository.SaveOutboxEventAsync(deadLetteredEvent);

        // Act
        var deadLetteredEvents = await _repository.GetDeadLetteredEventsAsync();

        // Assert
        Assert.Single(deadLetteredEvents);
        Assert.Equal(deadLetteredEvent.EventId, deadLetteredEvents.First().EventId);
    }

    [Fact]
    public async Task ReprocessDeadLetteredEventAsync_ShouldResetEventForReprocessing()
    {
        // Arrange
        var deadLetteredEvent = new OutboxEventEntity
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = Guid.NewGuid().ToString(),
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = "{}",
            Topic = "trades",
            PartitionKey = "partition1",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            IsDeadLettered = true,
            DeadLetteredAt = DateTime.UtcNow,
            DeadLetterReason = "Max retries exceeded",
            RetryCount = 3,
            LastRetryAt = DateTime.UtcNow,
            ErrorMessage = "Connection failed"
        };

        await _repository.SaveOutboxEventAsync(deadLetteredEvent);

        // Act
        await _repository.ReprocessDeadLetteredEventAsync(deadLetteredEvent.Id);

        // Assert
        var updatedEvent = await Context.OutboxEvents.FindAsync(deadLetteredEvent.Id);
        Assert.NotNull(updatedEvent);
        Assert.False(updatedEvent.IsDeadLettered);
        Assert.Null(updatedEvent.DeadLetteredAt);
        Assert.Null(updatedEvent.DeadLetterReason);
        Assert.False(updatedEvent.IsProcessed);
        Assert.Null(updatedEvent.ProcessedAt);
        Assert.Equal(0, updatedEvent.RetryCount);
        Assert.Null(updatedEvent.LastRetryAt);
        Assert.Null(updatedEvent.ErrorMessage);
    }

    [Fact]
    public async Task GetDeadLetteredEventCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var events = new[]
        {
            CreateOutboxEvent(isDeadLettered: true),
            CreateOutboxEvent(isDeadLettered: true),
            CreateOutboxEvent(isDeadLettered: false),
            CreateOutboxEvent(isDeadLettered: false)
        };

        foreach (var evt in events)
        {
            await _repository.SaveOutboxEventAsync(evt);
        }

        // Act
        var count = await _repository.GetDeadLetteredEventCountAsync();

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetUnprocessedEventsAsync_ShouldExcludeDeadLetteredEvents()
    {
        // Arrange
        var unprocessedEvent = CreateOutboxEvent(isProcessed: false, isDeadLettered: false);
        var deadLetteredEvent = CreateOutboxEvent(isProcessed: false, isDeadLettered: true);
        var processedEvent = CreateOutboxEvent(isProcessed: true, isDeadLettered: false);

        await _repository.SaveOutboxEventAsync(unprocessedEvent);
        await _repository.SaveOutboxEventAsync(deadLetteredEvent);
        await _repository.SaveOutboxEventAsync(processedEvent);

        // Act
        var unprocessedEvents = await _repository.GetUnprocessedEventsAsync();

        // Assert
        Assert.Single(unprocessedEvents);
        Assert.Equal(unprocessedEvent.EventId, unprocessedEvents.First().EventId);
    }

    [Fact]
    public async Task GetFailedEventsForRetryAsync_ShouldExcludeDeadLetteredEvents()
    {
        // Arrange
        var retryableEvent = CreateOutboxEvent(isProcessed: false, isDeadLettered: false, retryCount: 1);
        var deadLetteredEvent = CreateOutboxEvent(isProcessed: false, isDeadLettered: true, retryCount: 3);

        await _repository.SaveOutboxEventAsync(retryableEvent);
        await _repository.SaveOutboxEventAsync(deadLetteredEvent);

        // Act
        var retryableEvents = await _repository.GetFailedEventsForRetryAsync(TimeSpan.Zero, maxRetryCount: 3);

        // Assert
        Assert.Single(retryableEvents);
        Assert.Equal(retryableEvent.EventId, retryableEvents.First().EventId);
    }

    private OutboxEventEntity CreateOutboxEvent(
        bool isProcessed = false, 
        bool isDeadLettered = false, 
        int retryCount = 0)
    {
        var evt = new OutboxEventEntity
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = Guid.NewGuid().ToString(),
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = "{}",
            Topic = "trades",
            PartitionKey = "partition1",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = isProcessed,
            IsDeadLettered = isDeadLettered,
            RetryCount = retryCount
        };

        if (isProcessed)
        {
            evt.ProcessedAt = DateTime.UtcNow;
        }

        if (isDeadLettered)
        {
            evt.DeadLetteredAt = DateTime.UtcNow;
            evt.DeadLetterReason = "Test dead letter reason";
        }

        if (retryCount > 0)
        {
            evt.LastRetryAt = DateTime.UtcNow;
            evt.ErrorMessage = "Test error message";
        }

        return evt;
    }
}