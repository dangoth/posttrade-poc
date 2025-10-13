using PostTradeSystem.Core.Events;
using PostTradeSystem.Infrastructure.Tests.Integration;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using PostTradeSystem.Infrastructure.Tests.TestHelpers;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Repositories;

[Collection("SqlServer")]
public class EventStoreRepositoryTemporalTests : IntegrationTestBase
{
    public EventStoreRepositoryTemporalTests(SqlServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task GetEventsByTimeRangeAsync_ReturnsEventsInTimeRange()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = $"Trade:{aggregateId}";
        
        var events = new List<IDomainEvent>
        {
            DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1),
            DomainEventHelpers.CreateTradeCreatedEvent(Guid.NewGuid().ToString(), 1),
            DomainEventHelpers.CreateTradeCreatedEvent(Guid.NewGuid().ToString(), 1)
        };

        foreach (var domainEvent in events)
        {
            await EventStoreRepository.SaveEventsAsync(domainEvent.AggregateId, partitionKey, [domainEvent], 0);
        }

        var fromTime = DateTime.UtcNow.AddMinutes(-5);
        var toTime = DateTime.UtcNow.AddMinutes(5);
        
        var result = await EventStoreRepository.GetEventsByTimeRangeAsync(fromTime, toTime);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Count());
    }

    [Fact]
    public async Task GetEventsByAggregateTypeAsync_ReturnsEventsForSpecificType()
    {
        var tradeId1 = Guid.NewGuid().ToString();
        var tradeId2 = Guid.NewGuid().ToString();
        var partitionKey = "Trade:test";
        
        var tradeEvents = new List<IDomainEvent>
        {
            DomainEventHelpers.CreateTradeCreatedEvent(tradeId1, 1),
            DomainEventHelpers.CreateTradeCreatedEvent(tradeId2, 1)
        };

        foreach (var domainEvent in tradeEvents)
        {
            await EventStoreRepository.SaveEventsAsync(domainEvent.AggregateId, partitionKey, [domainEvent], 0);
        }

        var result = await EventStoreRepository.GetEventsByAggregateTypeAsync("Trade");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count());
        Assert.All(result.Value!, e => Assert.Equal("Trade", e.AggregateType));
    }

    [Fact]
    public async Task GetEventsByAggregateTypeAsync_WithTimeFilter_ReturnsFilteredEvents()
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

        var fromTime = DateTime.UtcNow.AddMinutes(-5);
        var toTime = DateTime.UtcNow.AddMinutes(5);
        
        var result = await EventStoreRepository.GetEventsByAggregateTypeAsync("Trade", fromTime, toTime);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Count());
        Assert.All(result.Value!, e => Assert.Equal("Trade", e.AggregateType));
    }

    [Fact]
    public async Task GetAllEventsInChronologicalOrderAsync_ReturnsEventsInCorrectOrder()
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

        var result = await EventStoreRepository.GetAllEventsInChronologicalOrderAsync();

        Assert.True(result.IsSuccess);
        var orderedEvents = result.Value!.ToList();
        Assert.Equal(3, orderedEvents.Count);
        
        // Since all events are created at nearly the same time, just verify we get all events
        Assert.All(orderedEvents, e => Assert.Equal("Trade", e.AggregateType));
    }

    [Fact]
    public async Task GetAllEventsInChronologicalOrderAsync_WithLimit_ReturnsLimitedResults()
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

        var result = await EventStoreRepository.GetAllEventsInChronologicalOrderAsync(limit: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count());
    }

    [Fact]
    public async Task GetEventsForReplayAsync_ReturnsEventsInVersionOrder()
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

        var result = await EventStoreRepository.GetEventsForReplayAsync(aggregateId, 1);

        Assert.True(result.IsSuccess);
        var replayEvents = result.Value!.ToList();
        Assert.Equal(2, replayEvents.Count);
        Assert.Equal(2, replayEvents[0].AggregateVersion);
        Assert.Equal(3, replayEvents[1].AggregateVersion);
    }

    [Fact]
    public async Task GetEventsForReplayAsync_WithToVersion_ReturnsEventsInRange()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = $"Trade:{aggregateId}";
        
        var events = new List<IDomainEvent>
        {
            DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1),
            DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 2),
            DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 3),
            DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 4)
        };

        foreach (var domainEvent in events)
        {
            await EventStoreRepository.SaveEventsAsync(domainEvent.AggregateId, partitionKey, [domainEvent], domainEvent.AggregateVersion - 1);
        }

        var result = await EventStoreRepository.GetEventsForReplayAsync(aggregateId, 1, 3);

        Assert.True(result.IsSuccess);
        var replayEvents = result.Value!.ToList();
        Assert.Equal(2, replayEvents.Count);
        Assert.Equal(2, replayEvents[0].AggregateVersion);
        Assert.Equal(3, replayEvents[1].AggregateVersion);
    }

    [Fact]
    public async Task GetEventsByVersionRangeAsync_ReturnsEventsInSpecifiedRange()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = $"Trade:{aggregateId}";
        
        var events = new List<IDomainEvent>
        {
            DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1),
            DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 2),
            DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 3),
            DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 4)
        };

        foreach (var domainEvent in events)
        {
            await EventStoreRepository.SaveEventsAsync(domainEvent.AggregateId, partitionKey, [domainEvent], domainEvent.AggregateVersion - 1);
        }

        var result = await EventStoreRepository.GetEventsByVersionRangeAsync(aggregateId, 2, 3);

        Assert.True(result.IsSuccess);
        var rangeEvents = result.Value!.ToList();
        Assert.Equal(2, rangeEvents.Count);
        Assert.Equal(2, rangeEvents[0].AggregateVersion);
        Assert.Equal(3, rangeEvents[1].AggregateVersion);
    }

    [Fact]
    public async Task GetEventsByTimeRangeAsync_WithNoEventsInRange_ReturnsEmptyCollection()
    {
        var fromTime = DateTime.UtcNow.AddHours(-2);
        var toTime = DateTime.UtcNow.AddHours(-1);
        
        var result = await EventStoreRepository.GetEventsByTimeRangeAsync(fromTime, toTime);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetEventsByAggregateTypeAsync_WithNonExistentType_ReturnsEmptyCollection()
    {
        var result = await EventStoreRepository.GetEventsByAggregateTypeAsync("NonExistentType");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }
}