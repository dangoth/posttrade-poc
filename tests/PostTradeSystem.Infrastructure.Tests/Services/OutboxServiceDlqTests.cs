using Microsoft.Extensions.Logging;
using Moq;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Infrastructure.Entities;
using PostTradeSystem.Infrastructure.Kafka;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Services;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Services;

public class OutboxServiceDlqTests
{
    private readonly Mock<IOutboxRepository> _mockRepository;
    private readonly Mock<IKafkaProducerService> _mockKafkaProducer;
    private readonly SerializationManagementService _serializationService;
    private readonly Mock<ILogger<OutboxService>> _mockLogger;
    private readonly OutboxService _outboxService;

    public OutboxServiceDlqTests()
    {
        _mockRepository = new Mock<IOutboxRepository>();
        _mockKafkaProducer = new Mock<IKafkaProducerService>();
        // Create real instances instead of mocks for concrete classes
        var registry = new EventSerializationRegistry();
        var mockSchemaRegistry = new Mock<ISchemaRegistry>();
        var validator = new JsonSchemaValidator();
        var mockTradeRiskService = new Mock<ITradeRiskService>();
        
        _serializationService = new SerializationManagementService(
            registry,
            mockSchemaRegistry.Object, 
            validator,
            mockTradeRiskService.Object);
        _mockLogger = new Mock<ILogger<OutboxService>>();

        _outboxService = new OutboxService(
            _mockRepository.Object,
            _mockKafkaProducer.Object,
            _serializationService,
            new RetryService(Mock.Of<ILogger<RetryService>>()),
            _mockLogger.Object);
    }

    [Fact]
    public async Task RetryFailedEventsAsync_ShouldMoveToDeadLetterAfterMaxRetries()
    {
        // Arrange
        var failedEvent = new OutboxEventEntity
        {
            Id = 1,
            EventId = Guid.NewGuid().ToString(),
            AggregateId = Guid.NewGuid().ToString(),
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = "{}",
            Topic = "trades",
            PartitionKey = "partition1",
            RetryCount = 2, // Will become 3 after increment, hitting max
            IsProcessed = false,
            IsDeadLettered = false
        };

        _mockRepository.Setup(r => r.GetFailedEventsForRetryAsync(It.IsAny<TimeSpan>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { failedEvent });

        _mockKafkaProducer.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Kafka connection failed"));

        // Act
        await _outboxService.RetryFailedEventsAsync();

        // Assert
        _mockRepository.Verify(r => r.IncrementRetryCountAsync(failedEvent.Id, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.MoveToDeadLetterAsync(failedEvent.Id, It.Is<string>(reason => reason.Contains("Exceeded max retry count")), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.MarkAsFailedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetryFailedEventsAsync_ShouldNotMoveToDeadLetterBeforeMaxRetries()
    {
        // Arrange
        var failedEvent = new OutboxEventEntity
        {
            Id = 1,
            EventId = Guid.NewGuid().ToString(),
            AggregateId = Guid.NewGuid().ToString(),
            AggregateType = "Trade",
            EventType = "TradeCreated",
            EventData = "{}",
            Metadata = "{}",
            Topic = "trades",
            PartitionKey = "partition1",
            RetryCount = 1, // Will become 2 after increment, below max of 3
            IsProcessed = false,
            IsDeadLettered = false
        };

        _mockRepository.Setup(r => r.GetFailedEventsForRetryAsync(It.IsAny<TimeSpan>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { failedEvent });

        _mockKafkaProducer.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Kafka connection failed"));

        // Act
        await _outboxService.RetryFailedEventsAsync();

        // Assert
        _mockRepository.Verify(r => r.IncrementRetryCountAsync(failedEvent.Id, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.MoveToDeadLetterAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepository.Verify(r => r.MarkAsFailedAsync(failedEvent.Id, "Kafka connection failed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDeadLetteredEventsAsync_ShouldReturnDeadLetteredEvents()
    {
        // Arrange
        var deadLetteredEvents = new[]
        {
            new OutboxEventEntity { Id = 1, EventId = "event1", IsDeadLettered = true },
            new OutboxEventEntity { Id = 2, EventId = "event2", IsDeadLettered = true }
        };

        _mockRepository.Setup(r => r.GetDeadLetteredEventsAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deadLetteredEvents);

        // Act
        var result = await _outboxService.GetDeadLetteredEventsAsync(100);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Equal("event1", result.First().EventId);
        Assert.Equal("event2", result.Last().EventId);
    }

    [Fact]
    public async Task ReprocessDeadLetteredEventAsync_ShouldCallRepositoryMethod()
    {
        // Arrange
        var eventId = 123L;

        // Act
        await _outboxService.ReprocessDeadLetteredEventAsync(eventId);

        // Assert
        _mockRepository.Verify(r => r.ReprocessDeadLetteredEventAsync(eventId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDeadLetteredEventCountAsync_ShouldReturnCount()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetDeadLetteredEventCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var count = await _outboxService.GetDeadLetteredEventCountAsync();

        // Assert
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task GetDeadLetteredEventsAsync_ShouldLogErrorAndRethrowOnException()
    {
        // Arrange
        var exception = new Exception("Database error");
        _mockRepository.Setup(r => r.GetDeadLetteredEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act & Assert
        var thrownException = await Assert.ThrowsAsync<Exception>(() => _outboxService.GetDeadLetteredEventsAsync(100));
        Assert.Equal("Database error", thrownException.Message);

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error retrieving dead lettered events")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ReprocessDeadLetteredEventAsync_ShouldLogErrorAndRethrowOnException()
    {
        // Arrange
        var eventId = 123L;
        var exception = new Exception("Database error");
        _mockRepository.Setup(r => r.ReprocessDeadLetteredEventAsync(eventId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act & Assert
        var thrownException = await Assert.ThrowsAsync<Exception>(() => _outboxService.ReprocessDeadLetteredEventAsync(eventId));
        Assert.Equal("Database error", thrownException.Message);

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error reprocessing dead lettered event {eventId}")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}