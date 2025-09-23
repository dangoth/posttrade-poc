using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Tests.Integration;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using PostTradeSystem.Infrastructure.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Repositories;

public class EventStoreRepositoryTests : IntegrationTestBase
{
    public EventStoreRepositoryTests(SqlServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task SaveEventsAsync_SavesEventsSuccessfully()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = "TRADER001:AAPL";
        var correlationId = Guid.NewGuid().ToString();
        var causedBy = "TestSystem";

        var domainEvent = DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1, correlationId, causedBy);
        var events = new List<IDomainEvent> { domainEvent };

        await EventStoreRepository.SaveEventsAsync(aggregateId, partitionKey, events, 0);

        var savedEvents = await Context.EventStore.ToListAsync();
        savedEvents.Should().HaveCount(1);
        
        var savedEvent = savedEvents.First();
        savedEvent.EventId.Should().Be(domainEvent.EventId);
        savedEvent.AggregateId.Should().Be(aggregateId);
        savedEvent.AggregateVersion.Should().Be(1);
        savedEvent.PartitionKey.Should().Be(partitionKey);
        savedEvent.CorrelationId.Should().Be(correlationId);
        savedEvent.CausedBy.Should().Be(causedBy);
        savedEvent.IsProcessed.Should().BeFalse();
    }

    [Fact]
    public async Task SaveEventsAsync_ThrowsOnConcurrencyConflict()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = "TRADER001:AAPL";
        
        var existingEvent = DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1, Guid.NewGuid().ToString(), "TestSystem");
        var events = new List<IDomainEvent> { existingEvent };

        await EventStoreRepository.SaveEventsAsync(aggregateId, partitionKey, events, 0);

        var newEvent = DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 2, Guid.NewGuid().ToString(), "TestSystem");
        var newEvents = new List<IDomainEvent> { newEvent };

        var act = async () => await EventStoreRepository.SaveEventsAsync(aggregateId, partitionKey, newEvents, 0);
        
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Concurrency conflict*");
    }

    [Fact]
    public async Task SaveEventsAsync_SkipsDuplicateEvents()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = "TRADER001:AAPL";
        var eventId = Guid.NewGuid().ToString();
        
        var domainEvent = DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1, Guid.NewGuid().ToString(), "TestSystem");
        // Set the specific eventId for this test
        typeof(Core.Events.DomainEventBase).GetProperty("EventId")!.SetValue(domainEvent, eventId);
        var events = new List<IDomainEvent> { domainEvent };

        await EventStoreRepository.SaveEventsAsync(aggregateId, partitionKey, events, 0);
        await EventStoreRepository.SaveEventsAsync(aggregateId, partitionKey, events, 1);

        var savedEvents = await Context.EventStore.ToListAsync();
        savedEvents.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetEventsAsync_ReturnsEventsInOrder()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = "TRADER001:AAPL";
        
        var event1 = DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1, Guid.NewGuid().ToString(), "TestSystem");
        var event2 = DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 2, Guid.NewGuid().ToString(), "TestSystem");
        var events = new List<IDomainEvent> { event1, event2 };

        await EventStoreRepository.SaveEventsAsync(aggregateId, partitionKey, events, 0);

        var retrievedEvents = await EventStoreRepository.GetEventsAsync(aggregateId);

        retrievedEvents.Should().HaveCount(2);
        retrievedEvents.First().AggregateVersion.Should().Be(1);
        retrievedEvents.Last().AggregateVersion.Should().Be(2);
    }

    [Fact]
    public async Task CheckIdempotencyAsync_ReturnsTrueForExistingKey()
    {
        var idempotencyKey = Guid.NewGuid().ToString();
        var requestHash = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();

        await EventStoreRepository.SaveIdempotencyAsync(
            idempotencyKey, 
            aggregateId, 
            requestHash, 
            "{\"response\": \"data\"}", 
            TimeSpan.FromHours(1));

        var result = await EventStoreRepository.CheckIdempotencyAsync(idempotencyKey, requestHash);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckIdempotencyAsync_ReturnsFalseForExpiredKey()
    {
        var idempotencyKey = Guid.NewGuid().ToString();
        var requestHash = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();

        await EventStoreRepository.SaveIdempotencyAsync(
            idempotencyKey, 
            aggregateId, 
            requestHash, 
            "{\"response\": \"data\"}", 
            TimeSpan.FromMilliseconds(-1));

        await Task.Delay(10);

        var result = await EventStoreRepository.CheckIdempotencyAsync(idempotencyKey, requestHash);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetIdempotentResponseAsync_ReturnsStoredResponse()
    {
        var idempotencyKey = Guid.NewGuid().ToString();
        var requestHash = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var responseData = "{\"response\": \"data\"}";

        await EventStoreRepository.SaveIdempotencyAsync(
            idempotencyKey, 
            aggregateId, 
            requestHash, 
            responseData, 
            TimeSpan.FromHours(1));

        var result = await EventStoreRepository.GetIdempotentResponseAsync(idempotencyKey, requestHash);

        result.Should().Be(responseData);
    }

    [Fact]
    public async Task MarkEventsAsProcessedAsync_UpdatesProcessedStatus()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = "TRADER001:AAPL";
        var domainEvent = DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1, Guid.NewGuid().ToString(), "TestSystem");
        var events = new List<IDomainEvent> { domainEvent };

        await EventStoreRepository.SaveEventsAsync(aggregateId, partitionKey, events, 0);

        await EventStoreRepository.MarkEventsAsProcessedAsync(new[] { domainEvent.EventId });

        var savedEvent = await Context.EventStore.FirstAsync();
        savedEvent.IsProcessed.Should().BeTrue();
        savedEvent.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUnprocessedEventsAsync_ReturnsOnlyUnprocessedEvents()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = "TRADER001:AAPL";
        
        var event1 = DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1, Guid.NewGuid().ToString(), "TestSystem");
        var event2 = DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 2, Guid.NewGuid().ToString(), "TestSystem");
        var events = new List<IDomainEvent> { event1, event2 };

        await EventStoreRepository.SaveEventsAsync(aggregateId, partitionKey, events, 0);
        await EventStoreRepository.MarkEventsAsProcessedAsync(new[] { event1.EventId });

        var unprocessedEvents = await EventStoreRepository.GetUnprocessedEventsAsync();

        unprocessedEvents.Should().HaveCount(1);
    }

}


