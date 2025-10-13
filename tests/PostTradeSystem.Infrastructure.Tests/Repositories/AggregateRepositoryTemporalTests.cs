using PostTradeSystem.Core.Aggregates;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Tests.Integration;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using PostTradeSystem.Infrastructure.Tests.TestHelpers;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Repositories;

[Collection("SqlServer")]
public class AggregateRepositoryTemporalTests : IntegrationTestBase
{
    private readonly AggregateRepository<TradeAggregate> _repository;

    public AggregateRepositoryTemporalTests(SqlServerFixture fixture) : base(fixture)
    {
        _repository = new AggregateRepository<TradeAggregate>(
            EventStoreRepository,
            TradeAggregate.FromHistory);
    }

    [Fact]
    public async Task GetByIdAtVersionAsync_ReturnsAggregateAtSpecificVersion()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = $"Trade:{aggregateId}";
        
        var events = new List<IDomainEvent>
        {
            DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1),
            DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 2),
            DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 3)
        };

        foreach (var domainEvent in events)
        {
            await EventStoreRepository.SaveEventsAsync(domainEvent.AggregateId, partitionKey, [domainEvent], domainEvent.AggregateVersion - 1);
        }

        var result = await _repository.GetByIdAtVersionAsync(aggregateId, 2);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        
        var aggregate = result.Value!;
        Assert.Equal(aggregateId, aggregate.Id);
        
        var uncommittedEvents = aggregate.GetUncommittedEvents();
        Assert.Empty(uncommittedEvents);
    }

    [Fact]
    public async Task GetByIdAtVersionAsync_WithVersionBeyondEvents_ReturnsAggregateWithAllEvents()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = $"Trade:{aggregateId}";
        
        var events = new List<IDomainEvent>
        {
            DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1),
            DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 2)
        };

        foreach (var domainEvent in events)
        {
            await EventStoreRepository.SaveEventsAsync(domainEvent.AggregateId, partitionKey, [domainEvent], domainEvent.AggregateVersion - 1);
        }

        var result = await _repository.GetByIdAtVersionAsync(aggregateId, 10);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        
        var aggregate = result.Value!;
        Assert.Equal(aggregateId, aggregate.Id);
    }

    [Fact]
    public async Task GetByIdAtTimeAsync_ReturnsAggregateAtSpecificTime()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = $"Trade:{aggregateId}";
        
        var events = new List<IDomainEvent>
        {
            DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1),
            DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 2),
            DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 3)
        };

        foreach (var domainEvent in events)
        {
            await EventStoreRepository.SaveEventsAsync(domainEvent.AggregateId, partitionKey, [domainEvent], domainEvent.AggregateVersion - 1);
        }

        var pointInTime = DateTime.UtcNow.AddMinutes(5);
        var result = await _repository.GetByIdAtTimeAsync(aggregateId, pointInTime);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        
        var aggregate = result.Value!;
        Assert.Equal(aggregateId, aggregate.Id);
    }

    [Fact]
    public async Task GetByIdAtTimeAsync_BeforeFirstEvent_ReturnsNull()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = $"Trade:{aggregateId}";
        
        var events = new List<IDomainEvent>
        {
            DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1)
        };

        foreach (var domainEvent in events)
        {
            await EventStoreRepository.SaveEventsAsync(domainEvent.AggregateId, partitionKey, [domainEvent], domainEvent.AggregateVersion - 1);
        }

        var pointInTime = DateTime.UtcNow.AddMinutes(-10);
        var result = await _repository.GetByIdAtTimeAsync(aggregateId, pointInTime);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task ReplayAggregatesFromEventsAsync_ReturnsMultipleAggregates()
    {
        var aggregateId1 = Guid.NewGuid().ToString();
        var aggregateId2 = Guid.NewGuid().ToString();
        var partitionKey = "Trade:test";
        
        var events = new List<IDomainEvent>
        {
            DomainEventHelpers.CreateTradeCreatedEvent(aggregateId1, 1),
            DomainEventHelpers.CreateTradeCreatedEvent(aggregateId2, 1),
            DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId1, 2),
            DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId2, 2)
        };

        foreach (var domainEvent in events)
        {
            await EventStoreRepository.SaveEventsAsync(domainEvent.AggregateId, partitionKey, [domainEvent], domainEvent.AggregateVersion - 1);
        }

        var aggregateIds = new[] { aggregateId1, aggregateId2 };
        var result = await _repository.ReplayAggregatesFromEventsAsync(aggregateIds);

        Assert.True(result.IsSuccess);
        var aggregates = result.Value!.ToList();
        Assert.Equal(2, aggregates.Count);
        
        Assert.Contains(aggregates, a => a.Id == aggregateId1);
        Assert.Contains(aggregates, a => a.Id == aggregateId2);
    }

    [Fact]
    public async Task ReplayAggregatesFromEventsAsync_WithFromVersion_ReturnsAggregatesFromSpecificVersion()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = $"Trade:{aggregateId}";
        
        var events = new List<IDomainEvent>
        {
            DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1),
            DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 2),
            DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 3)
        };

        foreach (var domainEvent in events)
        {
            await EventStoreRepository.SaveEventsAsync(domainEvent.AggregateId, partitionKey, [domainEvent], domainEvent.AggregateVersion - 1);
        }

        var aggregateIds = new[] { aggregateId };
        var result = await _repository.ReplayAggregatesFromEventsAsync(aggregateIds, fromVersion: 1);

        Assert.True(result.IsSuccess);
        var aggregates = result.Value!.ToList();
        Assert.Single(aggregates);
        
        var aggregate = aggregates.First();
        Assert.Equal(aggregateId, aggregate.Id);
    }

    [Fact]
    public async Task ReplayAggregatesFromEventsAsync_WithNonExistentAggregates_ReturnsEmptyCollection()
    {
        var nonExistentIds = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
        
        var result = await _repository.ReplayAggregatesFromEventsAsync(nonExistentIds);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task ReplayAggregatesFromEventsAsync_WithMixedExistentAndNonExistent_ReturnsOnlyExistentAggregates()
    {
        var existentId = Guid.NewGuid().ToString();
        var nonExistentId = Guid.NewGuid().ToString();
        var partitionKey = $"Trade:{existentId}";
        
        var events = new List<IDomainEvent>
        {
            DomainEventHelpers.CreateTradeCreatedEvent(existentId, 1)
        };

        foreach (var domainEvent in events)
        {
            await EventStoreRepository.SaveEventsAsync(domainEvent.AggregateId, partitionKey, [domainEvent], domainEvent.AggregateVersion - 1);
        }

        var aggregateIds = new[] { existentId, nonExistentId };
        var result = await _repository.ReplayAggregatesFromEventsAsync(aggregateIds);

        Assert.True(result.IsSuccess);
        var aggregates = result.Value!.ToList();
        Assert.Single(aggregates);
        Assert.Equal(existentId, aggregates.First().Id);
    }

    [Fact]
    public async Task GetByIdAtVersionAsync_WithNonExistentAggregate_ReturnsNull()
    {
        var nonExistentId = Guid.NewGuid().ToString();
        
        var result = await _repository.GetByIdAtVersionAsync(nonExistentId, 1);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetByIdAtTimeAsync_WithNonExistentAggregate_ReturnsNull()
    {
        var nonExistentId = Guid.NewGuid().ToString();
        var pointInTime = DateTime.UtcNow;
        
        var result = await _repository.GetByIdAtTimeAsync(nonExistentId, pointInTime);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetByIdAtVersionAsync_WithZeroVersion_ReturnsNull()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = $"Trade:{aggregateId}";
        
        var events = new List<IDomainEvent>
        {
            DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1)
        };

        foreach (var domainEvent in events)
        {
            await EventStoreRepository.SaveEventsAsync(domainEvent.AggregateId, partitionKey, [domainEvent], domainEvent.AggregateVersion - 1);
        }

        var result = await _repository.GetByIdAtVersionAsync(aggregateId, 0);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }
}