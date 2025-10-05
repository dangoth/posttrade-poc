using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Infrastructure.Kafka;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Services;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using PostTradeSystem.Infrastructure.Tests.TestHelpers;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Integration;

[Collection("SqlServer")]
public class OutboxDeadLetterIntegrationTests : IntegrationTestBase
{
    private readonly IOutboxService _outboxService;
    private readonly IOutboxRepository _outboxRepository;
    private readonly Mock<IKafkaProducerService> _mockKafkaProducer;
    private readonly MockTimeProvider _mockTimeProvider;

    public OutboxDeadLetterIntegrationTests(SqlServerFixture fixture) : base(fixture)
    {
        _mockKafkaProducer = new Mock<IKafkaProducerService>();
        _mockTimeProvider = new MockTimeProvider();
        
        // Create services with proper DI - use the same context as the base test
        _outboxRepository = new OutboxRepository(Context, _mockTimeProvider);
        _outboxService = new OutboxService(
            _outboxRepository,
            _mockKafkaProducer.Object,
            SerializationService,
            Mock.Of<ILogger<OutboxService>>(),
            _mockTimeProvider);
    }

    [Fact]
    public async Task EndToEnd_EventShouldMoveToDeadLetterAfterMaxRetries()
    {
        // Arrange
        var domainEvent = DomainEventHelpers.CreateTradeCreatedEvent();
        
        // Setup Kafka to always fail
        _mockKafkaProducer.Setup(p => p.ProduceAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<Dictionary<string, string>>(), 
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Kafka is down"));

        // Act - Save event to outbox
        await _outboxService.SaveEventToOutboxAsync(domainEvent, "trades", "partition1");

        // Verify event is in outbox
        var unprocessedEvents = await _outboxRepository.GetUnprocessedEventsAsync();
        Assert.Single(unprocessedEvents);
        var outboxEvent = unprocessedEvents.First();
        Assert.False(outboxEvent.IsDeadLettered);
        Assert.Equal(0, outboxEvent.RetryCount);

        // Act - Process outbox events (should fail and increment retry count)
        await _outboxService.ProcessOutboxEventsAsync();

        // Verify retry count incremented but not dead lettered yet
        var eventAfterFirstFail = await Context.OutboxEvents.FindAsync(outboxEvent.Id);
        Assert.NotNull(eventAfterFirstFail);
        Assert.False(eventAfterFirstFail.IsDeadLettered);
        Assert.Equal(1, eventAfterFirstFail.RetryCount);

        // Act - Retry failed events multiple times to exceed max retries
        // Advance time to bypass retry delay and use proper retry logic
        _mockTimeProvider.AdvanceMinutes(6); // Advance past 5-minute retry delay
        await _outboxService.RetryFailedEventsAsync(); // Retry count: 2
        
        _mockTimeProvider.AdvanceMinutes(6); // Advance past retry delay again
        await _outboxService.RetryFailedEventsAsync(); // Retry count: 3, should move to DLQ

        // Assert - Event should now be dead lettered
        var deadLetteredEvent = await Context.OutboxEvents.FindAsync(outboxEvent.Id);
        Assert.NotNull(deadLetteredEvent);
        Assert.True(deadLetteredEvent.IsDeadLettered);
        Assert.NotNull(deadLetteredEvent.DeadLetteredAt);
        Assert.Contains("Exceeded max retry count", deadLetteredEvent.DeadLetterReason);

        // Verify it appears in dead letter queries
        var deadLetteredEvents = await _outboxService.GetDeadLetteredEventsAsync(100);
        Assert.Single(deadLetteredEvents);
        Assert.Equal(outboxEvent.EventId, deadLetteredEvents.First().EventId);

        // Verify it doesn't appear in unprocessed queries
        var unprocessedAfterDlq = await _outboxRepository.GetUnprocessedEventsAsync();
        Assert.Empty(unprocessedAfterDlq);

        // Verify count is correct
        var dlqCount = await _outboxService.GetDeadLetteredEventCountAsync();
        Assert.Equal(1, dlqCount);
    }

    [Fact]
    public async Task ReprocessDeadLetteredEvent_ShouldResetAndAllowSuccessfulProcessing()
    {
        // Arrange - Create a dead lettered event
        var domainEvent = DomainEventHelpers.CreateTradeCreatedEvent();
        await _outboxService.SaveEventToOutboxAsync(domainEvent, "trades", "partition1");

        var unprocessedEvents = await _outboxRepository.GetUnprocessedEventsAsync();
        var outboxEvent = unprocessedEvents.First();

        // Move to dead letter manually
        await _outboxRepository.MoveToDeadLetterAsync(outboxEvent.Id, "Test dead letter");

        // Verify it's dead lettered
        var deadLetteredEvent = await Context.OutboxEvents.FindAsync(outboxEvent.Id);
        Assert.True(deadLetteredEvent!.IsDeadLettered);

        // Act - Reprocess the dead lettered event
        await _outboxService.ReprocessDeadLetteredEventAsync(outboxEvent.Id);

        // Verify it's reset for reprocessing
        var reprocessedEvent = await Context.OutboxEvents.FindAsync(outboxEvent.Id);
        Assert.NotNull(reprocessedEvent);
        Assert.False(reprocessedEvent.IsDeadLettered);
        Assert.Null(reprocessedEvent.DeadLetteredAt);
        Assert.Null(reprocessedEvent.DeadLetterReason);
        Assert.False(reprocessedEvent.IsProcessed);
        Assert.Equal(0, reprocessedEvent.RetryCount);

        // Setup Kafka to succeed this time
        _mockKafkaProducer.Setup(p => p.ProduceAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<Dictionary<string, string>>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Confluent.Kafka.DeliveryResult<string, string>());

        // Act - Process the event successfully
        await _outboxService.ProcessOutboxEventsAsync();

        // Assert - Event should now be processed successfully
        var processedEvent = await Context.OutboxEvents.FindAsync(outboxEvent.Id);
        Assert.NotNull(processedEvent);
        Assert.True(processedEvent.IsProcessed);
        Assert.NotNull(processedEvent.ProcessedAt);
        Assert.False(processedEvent.IsDeadLettered);

        // Verify Kafka was called
        _mockKafkaProducer.Verify(p => p.ProduceAsync(
            "trades",
            "partition1",
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MultipleEvents_ShouldHandleMixedScenarios()
    {
        // Arrange - Create multiple events
        var events = new[]
        {
            DomainEventHelpers.CreateTradeCreatedEvent(),
            DomainEventHelpers.CreateTradeCreatedEvent(),
            DomainEventHelpers.CreateTradeCreatedEvent()
        };

        foreach (var evt in events)
        {
            await _outboxService.SaveEventToOutboxAsync(evt, "trades", "partition1");
        }

        var unprocessedEvents = await _outboxRepository.GetUnprocessedEventsAsync();
        Assert.Equal(3, unprocessedEvents.Count());

        // Setup Kafka to fail for all events
        _mockKafkaProducer.Setup(p => p.ProduceAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<Dictionary<string, string>>(), 
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Kafka failure"));

        // Act - Process and retry until some move to DLQ
        // Use proper retry logic with time manipulation
        await _outboxService.ProcessOutboxEventsAsync(); // All fail, retry count = 1
        
        _mockTimeProvider.AdvanceMinutes(6); // Advance past retry delay
        await _outboxService.RetryFailedEventsAsync(); // All fail, retry count = 2
        
        _mockTimeProvider.AdvanceMinutes(6); // Advance past retry delay
        await _outboxService.RetryFailedEventsAsync(); // All fail, retry count = 3, move to DLQ

        // Assert - All should be dead lettered
        var dlqCount = await _outboxService.GetDeadLetteredEventCountAsync();
        Assert.Equal(3, dlqCount);

        var deadLetteredEvents = await _outboxService.GetDeadLetteredEventsAsync(100);
        Assert.Equal(3, deadLetteredEvents.Count());

        // Act - Reprocess one event
        var firstDeadLetter = deadLetteredEvents.First();
        await _outboxService.ReprocessDeadLetteredEventAsync(firstDeadLetter.Id);

        // Setup Kafka to succeed for reprocessed events
        _mockKafkaProducer.Setup(p => p.ProduceAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<Dictionary<string, string>>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Confluent.Kafka.DeliveryResult<string, string>());

        await _outboxService.ProcessOutboxEventsAsync();

        // Assert - One should be processed, two still dead lettered
        var finalDlqCount = await _outboxService.GetDeadLetteredEventCountAsync();
        Assert.Equal(2, finalDlqCount);

        var processedEvent = await Context.OutboxEvents.FindAsync(firstDeadLetter.Id);
        Assert.True(processedEvent!.IsProcessed);
        Assert.False(processedEvent.IsDeadLettered);
    }
}