using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Moq;
using PostTradeSystem.Core.Common;
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
            new RetryService(Mock.Of<ILogger<RetryService>>()),
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
        Assert.Single(unprocessedEvents.Value!);
        var outboxEvent = unprocessedEvents.Value!.First();
        Assert.False(outboxEvent.IsDeadLettered);
        Assert.Equal(0, outboxEvent.RetryCount);

        // Act - Process outbox events (should fail with retries and move to DLQ)
        // Use minimal delay for fast testing
        await _outboxService.ProcessOutboxEventsAsync();

        // Assert - Event should now be dead lettered after retry attempts
        var deadLetteredEvent = await Context.OutboxEvents.FindAsync(outboxEvent.Id);
        Assert.NotNull(deadLetteredEvent);
        Assert.True(deadLetteredEvent.IsDeadLettered);
        Assert.NotNull(deadLetteredEvent.DeadLetteredAt);
        Assert.Contains("Failed after retry attempts with exponential backoff", deadLetteredEvent.DeadLetterReason);

        // Verify it appears in dead letter queries
        var deadLetteredEvents = await _outboxService.GetDeadLetteredEventsAsync(100);
        Assert.Single(deadLetteredEvents.Value!);
        Assert.Equal(outboxEvent.EventId, deadLetteredEvents.Value!.First().EventId);

        // Verify it doesn't appear in unprocessed queries
        var unprocessedAfterDlqResult = await _outboxRepository.GetUnprocessedEventsAsync();
        Assert.True(unprocessedAfterDlqResult.IsSuccess);
        var unprocessedAfterDlq = unprocessedAfterDlqResult.Value!;
        Assert.Empty(unprocessedAfterDlq);

        // Verify count is correct
        var dlqCount = await _outboxService.GetDeadLetteredEventCountAsync();
        Assert.Equal(1, dlqCount.Value);
    }

    [Fact]
    public async Task ReprocessDeadLetteredEvent_ShouldResetAndAllowSuccessfulProcessing()
    {
        // Arrange - Create a dead lettered event
        var domainEvent = DomainEventHelpers.CreateTradeCreatedEvent();
        await _outboxService.SaveEventToOutboxAsync(domainEvent, "trades", "partition1");

        var unprocessedEvents = await _outboxRepository.GetUnprocessedEventsAsync();
        var outboxEvent = unprocessedEvents.Value!.First();

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
            .ReturnsAsync(Result<DeliveryResult<string, string>>.Success(new DeliveryResult<string, string>()));

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
            var saveResult = await _outboxService.SaveEventToOutboxAsync(evt, "trades", "partition1");
        Assert.True(saveResult.IsSuccess);
        }

        var unprocessedEventsResult = await _outboxRepository.GetUnprocessedEventsAsync();
        Assert.True(unprocessedEventsResult.IsSuccess);
        var unprocessedEvents = unprocessedEventsResult.Value!;
        Assert.Equal(3, unprocessedEvents.Count());

        // Setup Kafka to fail for all events
        _mockKafkaProducer.Setup(p => p.ProduceAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<Dictionary<string, string>>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DeliveryResult<string, string>>.Failure("Kafka failure"));

        // Act - Process events (should fail with retries and move to DLQ)
        // With the new RetryService, all retries happen within ProcessOutboxEventsAsync
        var processResult = await _outboxService.ProcessOutboxEventsAsync();
        Assert.True(processResult.IsSuccess);

        // Assert - All should be dead lettered
        var dlqCountResult = await _outboxService.GetDeadLetteredEventCountAsync();
        Assert.True(dlqCountResult.IsSuccess);
        var dlqCount = dlqCountResult.Value;
        Assert.Equal(3, dlqCount);

        var deadLetteredEventsResult = await _outboxService.GetDeadLetteredEventsAsync(100);
        Assert.True(deadLetteredEventsResult.IsSuccess);
        var deadLetteredEvents = deadLetteredEventsResult.Value!;
        Assert.Equal(3, deadLetteredEvents.Count());

        // Act - Reprocess one event
        var firstDeadLetter = deadLetteredEvents.First();
        var reprocessResult = await _outboxService.ReprocessDeadLetteredEventAsync(firstDeadLetter.Id);
        Assert.True(reprocessResult.IsSuccess);

        // Setup Kafka to succeed for reprocessed events
        _mockKafkaProducer.Setup(p => p.ProduceAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<Dictionary<string, string>>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DeliveryResult<string, string>>.Success(new DeliveryResult<string, string>()));

        await _outboxService.ProcessOutboxEventsAsync();

        // Assert - One should be processed, two still dead lettered
        var finalDlqCount = await _outboxService.GetDeadLetteredEventCountAsync();
        Assert.Equal(2, finalDlqCount.Value);

        var processedEvent = await Context.OutboxEvents.FindAsync(firstDeadLetter.Id);
        Assert.True(processedEvent!.IsProcessed);
        Assert.False(processedEvent.IsDeadLettered);
    }
}