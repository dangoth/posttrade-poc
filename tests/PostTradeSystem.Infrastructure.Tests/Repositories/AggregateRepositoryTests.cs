using Moq;
using PostTradeSystem.Core.Aggregates;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Tests.Integration;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using PostTradeSystem.Infrastructure.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Repositories;

public class AggregateRepositoryTests : SqlServerTestBase
{
    private readonly Mock<IEventStoreRepository> _mockEventStore;
    private readonly Mock<Func<string, string, IEnumerable<IDomainEvent>, TestAggregate>> _mockFactory;
    private AggregateRepository<TestAggregate> _repository = null!;

    public AggregateRepositoryTests(SqlServerFixture fixture) : base(fixture)
    {
        _mockEventStore = new Mock<IEventStoreRepository>();
        _mockFactory = new Mock<Func<string, string, IEnumerable<IDomainEvent>, TestAggregate>>();
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        
        // Reset mocks for each test
        _mockEventStore.Reset();
        _mockFactory.Reset();
        
        _repository = new AggregateRepository<TestAggregate>(_mockEventStore.Object, _mockFactory.Object);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenNoEvents()
    {
        var aggregateId = Guid.NewGuid().ToString();
        
        _mockEventStore.Setup(es => es.GetEventsAsync(aggregateId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDomainEvent>());

        var result = await _repository.GetByIdAsync(aggregateId);

        result.Should().BeNull();
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
            new TestDomainEvent(aggregateId, "Trade", 1, correlationId, causedBy),
            new TestDomainEvent(aggregateId, "Trade", 2, correlationId, causedBy)
        };

        var expectedAggregate = new TestAggregate(aggregateId, partitionKey);

        _mockEventStore.Setup(es => es.GetEventsAsync(aggregateId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        _mockFactory.Setup(f => f(aggregateId, It.IsAny<string>(), events))
            .Returns(expectedAggregate);

        var result = await _repository.GetByIdAsync(aggregateId);

        result.Should().Be(expectedAggregate);
        _mockFactory.Verify(f => f(aggregateId, It.IsAny<string>(), events), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_DoesNothingWhenNoUncommittedEvents()
    {
        var aggregate = new TestAggregate(Guid.NewGuid().ToString(), "TRADER001:AAPL");

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
        var partitionKey = "TRADER001:AAPL";
        var aggregate = new TestAggregate(aggregateId, partitionKey);
        
        var domainEvent = new TestDomainEvent(aggregateId, "Trade", 1, Guid.NewGuid().ToString(), "TestSystem");
        aggregate.AddUncommittedEvent(domainEvent);

        await _repository.SaveAsync(aggregate);

        _mockEventStore.Verify(es => es.SaveEventsAsync(
            aggregateId,
            partitionKey,
            It.IsAny<IEnumerable<IDomainEvent>>(),
            0,
            It.IsAny<CancellationToken>()), Times.Once);

        aggregate.GetUncommittedEvents().Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_WithIdempotency_ChecksIdempotencyFirst()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = "TRADER001:AAPL";
        var idempotencyKey = "KEY001";
        var requestHash = "HASH001";
        var aggregate = new TestAggregate(aggregateId, partitionKey);
        
        var domainEvent = new TestDomainEvent(aggregateId, "Trade", 1, Guid.NewGuid().ToString(), "TestSystem");
        aggregate.AddUncommittedEvent(domainEvent);

        _mockEventStore.Setup(es => es.CheckIdempotencyAsync(idempotencyKey, requestHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _repository.SaveAsync(aggregate, idempotencyKey, requestHash);

        _mockEventStore.Verify(es => es.SaveEventsAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<IEnumerable<IDomainEvent>>(), 
            It.IsAny<long>(), 
            It.IsAny<CancellationToken>()), Times.Never);

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
        var partitionKey = "TRADER001:AAPL";
        var idempotencyKey = "KEY001";
        var requestHash = "HASH001";
        var aggregate = new TestAggregate(aggregateId, partitionKey);
        
        var domainEvent = new TestDomainEvent(aggregateId, "Trade", 1, Guid.NewGuid().ToString(), "TestSystem");
        aggregate.AddUncommittedEvent(domainEvent);

        _mockEventStore.Setup(es => es.CheckIdempotencyAsync(idempotencyKey, requestHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _repository.SaveAsync(aggregate, idempotencyKey, requestHash);

        _mockEventStore.Verify(es => es.SaveEventsAsync(
            aggregateId,
            partitionKey,
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

public class TestAggregate : IAggregateRoot
{
    private readonly List<IDomainEvent> _uncommittedEvents = new();

    public TestAggregate(string id, string partitionKey)
    {
        Id = id;
        PartitionKey = partitionKey;
    }

    public string Id { get; }
    public string PartitionKey { get; }

    public IReadOnlyList<IDomainEvent> GetUncommittedEvents()
    {
        return _uncommittedEvents.AsReadOnly();
    }

    public void MarkEventsAsCommitted()
    {
        _uncommittedEvents.Clear();
    }

    public void LoadFromHistory(IEnumerable<IDomainEvent> events)
    {
        // Test implementation - just load events without applying them
    }

    public void AddUncommittedEvent(IDomainEvent domainEvent)
    {
        _uncommittedEvents.Add(domainEvent);
    }
}