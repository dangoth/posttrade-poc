using Moq;
using Microsoft.Extensions.DependencyInjection;
using PostTradeSystem.Core.Aggregates;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Tests.Integration;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using PostTradeSystem.Infrastructure.Tests.TestHelpers;
using FluentAssertions;
using Xunit;
using PostTradeSystem.Core.Common;

namespace PostTradeSystem.Infrastructure.Tests.Repositories;

public class AggregateRepositoryTests : IntegrationTestBase
{
    private readonly Mock<IEventStoreRepository> _mockEventStore;
    private readonly Mock<Func<string, string, IEnumerable<IDomainEvent>, TradeAggregate>> _mockFactory;
    private AggregateRepository<TradeAggregate> _repository = null!;

    public AggregateRepositoryTests(SqlServerFixture fixture) : base(fixture)
    {
        _mockEventStore = new Mock<IEventStoreRepository>();
        _mockFactory = new Mock<Func<string, string, IEnumerable<IDomainEvent>, TradeAggregate>>();
        _repository = new AggregateRepository<TradeAggregate>(_mockEventStore.Object, _mockFactory.Object);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenNoEvents()
    {
        var aggregateId = Guid.NewGuid().ToString();
        
        _mockEventStore.Setup(es => es.GetEventsAsync(aggregateId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<IDomainEvent>>.Success(new List<IDomainEvent>()));

        var result = await _repository.GetByIdAsync(aggregateId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsAggregateWhenEventsExist()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = "TRADER001:AAPL";
        var correlationId = Guid.NewGuid().ToString();
        var causedBy = "TestSystem";
        
        var events = new List<IDomainEvent>
        {
            DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1, correlationId, causedBy),
            DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 2, correlationId, causedBy)
        };

        var expectedAggregate = TradeAggregate.FromHistory(aggregateId, partitionKey, events);

        _mockEventStore.Setup(es => es.GetEventsAsync(aggregateId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<IDomainEvent>>.Success(events));

        _mockFactory.Setup(f => f(aggregateId, It.IsAny<string>(), events))
            .Returns(expectedAggregate);

        var result = await _repository.GetByIdAsync(aggregateId);

        result.Should().NotBeNull();
        result!.Value!.Id.Should().Be(aggregateId);
        result.Value.PartitionKey.Should().Be(partitionKey);
        _mockFactory.Verify(f => f(aggregateId, It.IsAny<string>(), events), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_DoesNothingWhenNoUncommittedEvents()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = "TRADER001:AAPL";
        
        // Create an aggregate with no uncommitted events (just from history)
        var initialEvent = DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1, Guid.NewGuid().ToString(), "TestSystem");
        var aggregate = TradeAggregate.FromHistory(aggregateId, partitionKey, new[] { initialEvent });

        await _repository.SaveAsync(aggregate);

        _mockEventStore.Verify(es => es.SaveEventsAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<IEnumerable<IDomainEvent>>(), 
            It.IsAny<long>(), 
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_SavesUncommittedEvents()
    {
        var aggregateId = Guid.NewGuid().ToString();
        
        // Create a new trade aggregate which will have uncommitted events
        var aggregate = TradeAggregate.CreateTrade(
            aggregateId,
            "TRADER001",
            "AAPL", 
            100m,
            150.50m,
            PostTradeSystem.Core.Models.TradeDirection.Buy,
            DateTime.UtcNow,
            "USD",
            "COUNTERPARTY001",
            "EQUITY",
            new Dictionary<string, object>(),
            Guid.NewGuid().ToString(),
            "TestSystem"
        );

        _mockEventStore.Setup(es => es.SaveEventsAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<IDomainEvent>>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _repository.SaveAsync(aggregate);

        result.IsSuccess.Should().BeTrue();
        _mockEventStore.Verify(es => es.SaveEventsAsync(
            aggregateId,
            aggregate.PartitionKey,
            It.IsAny<IEnumerable<IDomainEvent>>(),
            0,
            It.IsAny<CancellationToken>()), Times.Once);

        aggregate.GetUncommittedEvents().Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_WithIdempotency_ChecksIdempotencyFirst()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var idempotencyKey = "KEY001";
        var requestHash = "HASH001";
        // Create a new trade aggregate which will have uncommitted events
        var aggregate = TradeAggregate.CreateTrade(
            aggregateId,
            "TRADER001",
            "AAPL", 
            100m,
            150.50m,
            PostTradeSystem.Core.Models.TradeDirection.Buy,
            DateTime.UtcNow,
            "USD",
            "COUNTERPARTY001",
            "EQUITY",
            new Dictionary<string, object>(),
            Guid.NewGuid().ToString(),
            "TestSystem"
        );

        _mockEventStore.Setup(es => es.CheckIdempotencyAsync(idempotencyKey, requestHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));

        await _repository.SaveAsync(aggregate, idempotencyKey, requestHash);

        _mockEventStore.Verify(es => es.SaveEventsAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<IEnumerable<IDomainEvent>>(), 
            It.IsAny<long>(), 
            It.IsAny<CancellationToken>()), Times.Never);

        _mockEventStore.Setup(es => es.SaveIdempotencyAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<TimeSpan>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _mockEventStore.Verify(es => es.SaveIdempotencyAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<TimeSpan>(), 
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_WithIdempotency_SavesWhenNotIdempotent()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var idempotencyKey = "KEY001";
        var requestHash = "HASH001";
        // Create a new trade aggregate which will have uncommitted events
        var aggregate = TradeAggregate.CreateTrade(
            aggregateId,
            "TRADER001",
            "AAPL", 
            100m,
            150.50m,
            PostTradeSystem.Core.Models.TradeDirection.Buy,
            DateTime.UtcNow,
            "USD",
            "COUNTERPARTY001",
            "EQUITY",
            new Dictionary<string, object>(),
            Guid.NewGuid().ToString(),
            "TestSystem"
        );

        _mockEventStore.Setup(es => es.CheckIdempotencyAsync(idempotencyKey, requestHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(false));
        
        _mockEventStore.Setup(es => es.SaveEventsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
            
        _mockEventStore.Setup(es => es.SaveIdempotencyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        await _repository.SaveAsync(aggregate, idempotencyKey, requestHash);

        _mockEventStore.Verify(es => es.SaveEventsAsync(
            aggregateId,
            aggregate.PartitionKey,
            It.IsAny<IEnumerable<IDomainEvent>>(),
            0,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockEventStore.Verify(es => es.SaveIdempotencyAsync(
            idempotencyKey,
            aggregateId,
            requestHash,
            It.IsAny<string>(),
            TimeSpan.FromHours(24),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

