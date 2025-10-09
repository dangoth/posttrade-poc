using Microsoft.Extensions.Logging;
using Moq;
using PostTradeSystem.Core.Common;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Infrastructure.Entities;
using PostTradeSystem.Infrastructure.Kafka;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Services;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Services;

public class OutboxServiceTests
{
    private readonly Mock<IOutboxRepository> _mockOutboxRepository;
    private readonly Mock<IKafkaProducerService> _mockKafkaProducer;
    private readonly ISerializationManagementService _realSerializationService;
    private readonly Mock<ILogger<OutboxService>> _mockLogger;
    private readonly OutboxService _outboxService;

    public OutboxServiceTests()
    {
        _mockOutboxRepository = new Mock<IOutboxRepository>();
        
        // Create a mock that will simulate failure (30 second timeout)
        _mockKafkaProducer = new Mock<IKafkaProducerService>();
        _mockKafkaProducer
            .Setup(x => x.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Confluent.Kafka.DeliveryResult<string, string>>.Failure("Kafka connection timeout")));
            
        var mockSerializationService = new Mock<ISerializationManagementService>();
        mockSerializationService.Setup(x => x.SerializeAsync(It.IsAny<IDomainEvent>(), It.IsAny<int?>()))
            .ReturnsAsync(Result<SerializedEvent>.Success(new SerializedEvent("TradeCreated", 1, "{}", "schema1", DateTime.UtcNow, new Dictionary<string, string>())));
        
        _realSerializationService = mockSerializationService.Object;
            
        // SerializationService is now mocked, no need to initialize
            
        _mockLogger = new Mock<ILogger<OutboxService>>();

        _outboxService = new OutboxService(
            _mockOutboxRepository.Object,
            _mockKafkaProducer.Object,
            _realSerializationService,
            new RetryService(Mock.Of<ILogger<RetryService>>()),
            _mockLogger.Object);
    }

    [Fact]
    public async Task SaveEventToOutboxAsync_ShouldSerializeAndSaveEvent()
    {
        // Arrange
        var domainEvent = new TradeCreatedEvent(
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
            "TestService",
            new Dictionary<string, object>());

        // Setup mock before calling the method
        _mockOutboxRepository.Setup(x => x.SaveOutboxEventAsync(It.IsAny<OutboxEventEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        Console.WriteLine($"[DEBUG] Saving event to outbox: {domainEvent.EventId}");
        var result = await _outboxService.SaveEventToOutboxAsync(domainEvent, "events.trades", "partition-key");
        Console.WriteLine("[DEBUG] SaveEventToOutboxAsync completed");
        
        // Assert result is successful
        Assert.True(result.IsSuccess);

        _mockOutboxRepository.Verify(x => x.SaveOutboxEventAsync(
            It.Is<OutboxEventEntity>(e => 
                e.EventId == domainEvent.EventId &&
                e.AggregateId == domainEvent.AggregateId &&
                e.EventType == "TradeCreated" &&
                e.Topic == "events.trades" &&
                e.PartitionKey == "partition-key" &&
                !e.IsProcessed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOutboxEventsAsync_ShouldProcessUnprocessedEvents()
    {
        // Arrange
        var outboxEvents = new List<OutboxEventEntity>
        {
            new OutboxEventEntity
            {
                Id = 1,
                EventId = "event-1",
                AggregateId = "TRADE-001",
                AggregateType = "Trade",
                EventType = "TradeCreated",
                EventData = "{\"tradeId\":\"TRADE-001\"}",
                Metadata = "{\"version\":\"1.0\"}",
                Topic = "events.trades",
                PartitionKey = "partition-key",
                CreatedAt = DateTime.UtcNow,
                IsProcessed = false
            }
        };

        _mockOutboxRepository
            .Setup(x => x.GetUnprocessedEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<OutboxEventEntity>>.Success(outboxEvents));

        // Setup mock returns for repository methods before calling the method
        _mockOutboxRepository.Setup(x => x.MoveToDeadLetterAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _outboxService.ProcessOutboxEventsAsync();
        
        // Assert result is successful
        Assert.True(result.IsSuccess);

        // Since Kafka connection fails (timeout), the event should be moved to dead letter queue after retry attempts
        _mockOutboxRepository.Verify(x => x.MoveToDeadLetterAsync(1, 
            It.Is<string>(reason => reason.Contains("Failed after retry attempts with exponential backoff")), 
            It.IsAny<CancellationToken>()), Times.Once);
        
        // Verify the Kafka producer was called multiple times due to retry attempts (3 retries + 1 initial = 4 total)
        _mockKafkaProducer.Verify(x => x.ProduceAsync(
            "events.trades",
            "partition-key", 
            "{\"tradeId\":\"TRADE-001\"}",
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(4));
    }
}

