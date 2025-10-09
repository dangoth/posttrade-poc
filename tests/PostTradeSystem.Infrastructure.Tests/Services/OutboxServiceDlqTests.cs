using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Moq;
using PostTradeSystem.Core.Common;
using PostTradeSystem.Core.Events;
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
    private readonly ISerializationManagementService _serializationService;
    private readonly Mock<ILogger<OutboxService>> _mockLogger;
    private readonly OutboxService _outboxService;

    public OutboxServiceDlqTests()
    {
        _mockRepository = new Mock<IOutboxRepository>();
        _mockKafkaProducer = new Mock<IKafkaProducerService>();
        // Create mock serialization service that returns Results
        var mockSerializationService = new Mock<ISerializationManagementService>();
        mockSerializationService.Setup(x => x.SerializeAsync(It.IsAny<IDomainEvent>(), It.IsAny<int?>()))
            .ReturnsAsync(Result<SerializedEvent>.Success(new SerializedEvent("TradeCreated", 1, "{}", "schema1", DateTime.UtcNow, new Dictionary<string, string>())));
        
        _serializationService = mockSerializationService.Object;
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
            .ReturnsAsync(Result<IEnumerable<OutboxEventEntity>>.Success(new[] { failedEvent }));
        _mockRepository.Setup(r => r.IncrementRetryCountAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _mockRepository.Setup(r => r.MoveToDeadLetterAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _mockKafkaProducer.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DeliveryResult<string, string>>.Failure("Kafka connection failed"));

        // Act
        var result = await _outboxService.RetryFailedEventsAsync();

        // Assert
        Assert.True(result.IsSuccess);
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
            .ReturnsAsync(Result<IEnumerable<OutboxEventEntity>>.Success(new[] { failedEvent }));
        _mockRepository.Setup(r => r.IncrementRetryCountAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _mockRepository.Setup(r => r.MarkAsFailedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _mockKafkaProducer.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DeliveryResult<string, string>>.Failure("Kafka connection failed"));

        // Act
        var result = await _outboxService.RetryFailedEventsAsync();

        // Assert
        Assert.True(result.IsSuccess);
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
            .ReturnsAsync(Result<IEnumerable<OutboxEventEntity>>.Success(deadLetteredEvents));

        // Act
        var result = await _outboxService.GetDeadLetteredEventsAsync(100);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count());
        Assert.Equal("event1", result.Value.First().EventId);
        Assert.Equal("event2", result.Value.Last().EventId);
    }

    [Fact]
    public async Task ReprocessDeadLetteredEventAsync_ShouldCallRepositoryMethod()
    {
        // Arrange
        var eventId = 123L;

        // Setup
        _mockRepository.Setup(r => r.ReprocessDeadLetteredEventAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _outboxService.ReprocessDeadLetteredEventAsync(eventId);

        // Assert
        Assert.True(result.IsSuccess);
        _mockRepository.Verify(r => r.ReprocessDeadLetteredEventAsync(eventId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDeadLetteredEventCountAsync_ShouldReturnCount()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetDeadLetteredEventCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(5));

        // Act
        var result = await _outboxService.GetDeadLetteredEventCountAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public async Task GetDeadLetteredEventsAsync_ShouldLogErrorAndRethrowOnException()
    {
        // Arrange
        var exception = new Exception("Database error");
        _mockRepository.Setup(r => r.GetDeadLetteredEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<OutboxEventEntity>>.Failure("Database error"));

        // Act
        var result = await _outboxService.GetDeadLetteredEventsAsync(100);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Database error", result.Error);

        // Verify error was NOT logged (since repository returned failure Result, not exception)
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
    public async Task ReprocessDeadLetteredEventAsync_ShouldLogErrorAndRethrowOnException()
    {
        // Arrange
        var eventId = 123L;
        var exception = new Exception("Database error");
        _mockRepository.Setup(r => r.ReprocessDeadLetteredEventAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Database error"));

        // Act
        var result = await _outboxService.ReprocessDeadLetteredEventAsync(eventId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Database error", result.Error);

        // Verify error was NOT logged (since repository returned failure Result, not exception)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}