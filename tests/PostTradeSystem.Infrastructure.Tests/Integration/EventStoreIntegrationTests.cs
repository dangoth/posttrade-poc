using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using PostTradeSystem.Infrastructure.Tests.TestHelpers;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Integration;

[Collection("SqlServer")]
public class EventStoreIntegrationTests : IntegrationTestBase
{
    public EventStoreIntegrationTests(SqlServerFixture fixture) : base(fixture)
    {
    }


    [Fact]
    public async Task EventStore_FullWorkflow_WithSqlServerDatabase()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = "TRADER001:AAPL";
        var correlationId = Guid.NewGuid().ToString();
        var causedBy = "IntegrationTest";

        var event1 = DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1, correlationId, causedBy);
        var event2 = DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 2, correlationId, causedBy);
        var events = new List<IDomainEvent> { event1, event2 };

        await EventStoreRepository.SaveEventsAsync(aggregateId, partitionKey, events, 0);

        var retrievedEvents = await EventStoreRepository.GetEventsAsync(aggregateId);

        retrievedEvents.Value!.Should().HaveCount(2);
        retrievedEvents.Value!.Should().BeInAscendingOrder(e => e.AggregateVersion);

        var eventsByPartition = await EventStoreRepository.GetEventsByPartitionKeyAsync(partitionKey);
        eventsByPartition.Value!.Should().HaveCount(2);

        var unprocessedEvents = await EventStoreRepository.GetUnprocessedEventsAsync();
        unprocessedEvents.Value!.Should().HaveCount(2);

        await EventStoreRepository.MarkEventsAsProcessedAsync(new[] { event1.EventId });

        var remainingUnprocessed = await EventStoreRepository.GetUnprocessedEventsAsync();
        remainingUnprocessed.Value!.Should().HaveCount(1);
    }

    [Fact]
    public async Task EventStore_IdempotencyWorkflow_WithSqlServerDatabase()
    {
        var idempotencyKey = Guid.NewGuid().ToString();
        var requestHash = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var responseData = "{\"success\": true}";

        var isIdempotentBefore = await EventStoreRepository.CheckIdempotencyAsync(idempotencyKey, requestHash);
        isIdempotentBefore.Value.Should().BeFalse();

        await EventStoreRepository.SaveIdempotencyAsync(
            idempotencyKey, 
            aggregateId, 
            requestHash, 
            responseData, 
            TimeSpan.FromHours(1));

        var isIdempotentAfter = await EventStoreRepository.CheckIdempotencyAsync(idempotencyKey, requestHash);
        isIdempotentAfter.Value.Should().BeTrue();

        var retrievedResponseResult = await EventStoreRepository.GetIdempotentResponseAsync(idempotencyKey, requestHash);
        retrievedResponseResult.IsSuccess.Should().BeTrue();
        retrievedResponseResult.Value.Should().Be(responseData);

        var wrongHashResponseResult = await EventStoreRepository.GetIdempotentResponseAsync(idempotencyKey, "WRONG_HASH");
        wrongHashResponseResult.IsSuccess.Should().BeTrue();
        wrongHashResponseResult.Value.Should().BeNull();
    }

    [Fact]
    public async Task EventStore_ConcurrencyControl_WithSqlServerDatabase()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = "TRADER001:AAPL";
        var correlationId = Guid.NewGuid().ToString();

        var event1 = DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1, correlationId, "System1");
        var event2 = DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 2, correlationId, "System2");

        await EventStoreRepository.SaveEventsAsync(aggregateId, partitionKey, new[] { event1 }, 0);

        var result = await EventStoreRepository.SaveEventsAsync(aggregateId, partitionKey, new[] { event2 }, 0);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Concurrency conflict");
    }

    [Fact]
    public async Task EventStore_SqlServerConstraints_EnforceUniqueness()
    {
        var eventId = Guid.NewGuid().ToString();
        
        var entity1 = new PostTradeSystem.Infrastructure.Entities.EventStoreEntity
        {
            EventId = eventId,
            AggregateId = Guid.NewGuid().ToString(),
            AggregateType = "Trade",
            PartitionKey = "TRADER001:AAPL",
            AggregateVersion = 1,
            EventType = "TestEvent",
            EventData = "{}",
            OccurredAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString(),
            CausedBy = "Test"
        };

        var entity2 = new PostTradeSystem.Infrastructure.Entities.EventStoreEntity
        {
            EventId = eventId,
            AggregateId = Guid.NewGuid().ToString(), 
            AggregateType = "Trade",
            PartitionKey = "TRADER002:AAPL",
            AggregateVersion = 1,
            EventType = "TestEvent",
            EventData = "{}",
            OccurredAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString(),
            CausedBy = "Test"
        };

        Context.EventStore.Add(entity1);
        await Context.SaveChangesAsync();

        Context.EventStore.Add(entity2);

        var act = async () => await Context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task EventStore_SqlServerTransactions_RollbackOnError()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = "TRADER001:AAPL";
        var correlationId = Guid.NewGuid().ToString();
        var sharedEventId = Guid.NewGuid().ToString();

        var validEvent = DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, 1, correlationId, "TestSystem");
        
        // Create an event with the same EventId to cause a unique constraint violation
        var duplicateEvent = DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, 2, correlationId, "TestSystem");
        
        // Force the same EventId to test unique constraint
        typeof(Core.Events.DomainEventBase).GetProperty("EventId")!.SetValue(duplicateEvent, validEvent.EventId);

        var events = new List<IDomainEvent> { validEvent, duplicateEvent };

        var result = await EventStoreRepository.SaveEventsAsync(aggregateId, partitionKey, events, 0);

        result.IsSuccess.Should().BeFalse();

        var savedEvents = await Context.EventStore.CountAsync();
        savedEvents.Should().Be(0);
    }

    [Fact]
    public async Task EventStore_SqlServerIndexes_PerformanceOptimized()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = "TRADER001:AAPL";
        
        var events = new List<IDomainEvent>();
        for (int i = 1; i <= 100; i++)
        {
            // Alternate between different event types for more realistic testing
            if (i % 3 == 1)
                events.Add(DomainEventHelpers.CreateTradeCreatedEvent(aggregateId, i, Guid.NewGuid().ToString(), "TestSystem"));
            else if (i % 3 == 2)
                events.Add(DomainEventHelpers.CreateTradeStatusChangedEvent(aggregateId, i, Guid.NewGuid().ToString(), "TestSystem"));
            else
                events.Add(DomainEventHelpers.CreateTradeUpdatedEvent(aggregateId, i, Guid.NewGuid().ToString(), "TestSystem"));
        }

        var startTime = DateTime.UtcNow;
        
        foreach (var eventBatch in events.Chunk(10))
        {
            await EventStoreRepository.SaveEventsAsync(aggregateId, partitionKey, eventBatch, eventBatch.First().AggregateVersion - 1);
        }

        var retrievedEvents = await EventStoreRepository.GetEventsAsync(aggregateId);
        
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        retrievedEvents.Value.Should().HaveCount(100);
        duration.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }
}