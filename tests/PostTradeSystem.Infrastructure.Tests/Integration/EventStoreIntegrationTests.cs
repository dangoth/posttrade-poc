using Microsoft.EntityFrameworkCore;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Tests.TestHelpers;
using FluentAssertions;
using Moq;
using Testcontainers.MsSql;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Integration;

[Collection("SqlServer")]
public class EventStoreIntegrationTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private PostTradeDbContext _context => _fixture.Context;
    private Mock<IEventSerializer> _mockSerializer = null!;
    private EventStoreRepository _repository = null!;

    public EventStoreIntegrationTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Reset database to clean state before each test
        await _fixture.ResetDatabaseAsync();

        _mockSerializer = new Mock<IEventSerializer>();
        _repository = new EventStoreRepository(_context, _mockSerializer.Object);
    }

    public async Task DisposeAsync()
    {
        // Nothing to dispose - fixture handles the context
        await Task.CompletedTask;
    }

    [Fact]
    public async Task EventStore_FullWorkflow_WithSqlServerDatabase()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = "TRADER001:AAPL";
        var correlationId = Guid.NewGuid().ToString();
        var causedBy = "IntegrationTest";

        var event1 = new TestDomainEvent(aggregateId, "Trade", 1, correlationId, causedBy);
        var event2 = new TestDomainEvent(aggregateId, "Trade", 2, correlationId, causedBy);
        var events = new List<IDomainEvent> { event1, event2 };

        _mockSerializer.Setup(s => s.Serialize(It.Is<IDomainEvent>(e => e.AggregateVersion == 1)))
            .ReturnsAsync(new SerializedEvent("TestDomainEvent", 1, "{\"AggregateVersion\":1}", "test-schema", DateTime.UtcNow, new Dictionary<string, string>()));
        
        _mockSerializer.Setup(s => s.Serialize(It.Is<IDomainEvent>(e => e.AggregateVersion == 2)))
            .ReturnsAsync(new SerializedEvent("TestDomainEvent", 1, "{\"AggregateVersion\":2}", "test-schema", DateTime.UtcNow, new Dictionary<string, string>()));

        _mockSerializer.Setup(s => s.Deserialize(It.IsAny<SerializedEvent>()))
            .Returns((SerializedEvent se) => 
                se.Data.Contains("\"AggregateVersion\":1") ? event1 : event2);

        await _repository.SaveEventsAsync(aggregateId, partitionKey, events, 0);

        var retrievedEvents = await _repository.GetEventsAsync(aggregateId);

        retrievedEvents.Should().HaveCount(2);
        retrievedEvents.Should().BeInAscendingOrder(e => e.AggregateVersion);

        var eventsByPartition = await _repository.GetEventsByPartitionKeyAsync(partitionKey);
        eventsByPartition.Should().HaveCount(2);

        var unprocessedEvents = await _repository.GetUnprocessedEventsAsync();
        unprocessedEvents.Should().HaveCount(2);

        await _repository.MarkEventsAsProcessedAsync(new[] { event1.EventId });

        var remainingUnprocessed = await _repository.GetUnprocessedEventsAsync();
        remainingUnprocessed.Should().HaveCount(1);
    }

    [Fact]
    public async Task EventStore_IdempotencyWorkflow_WithSqlServerDatabase()
    {
        var idempotencyKey = Guid.NewGuid().ToString();
        var requestHash = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var responseData = "{\"success\": true}";

        var isIdempotentBefore = await _repository.CheckIdempotencyAsync(idempotencyKey, requestHash);
        isIdempotentBefore.Should().BeFalse();

        await _repository.SaveIdempotencyAsync(
            idempotencyKey, 
            aggregateId, 
            requestHash, 
            responseData, 
            TimeSpan.FromHours(1));

        var isIdempotentAfter = await _repository.CheckIdempotencyAsync(idempotencyKey, requestHash);
        isIdempotentAfter.Should().BeTrue();

        var retrievedResponse = await _repository.GetIdempotentResponseAsync(idempotencyKey, requestHash);
        retrievedResponse.Should().Be(responseData);

        var wrongHashResponse = await _repository.GetIdempotentResponseAsync(idempotencyKey, "WRONG_HASH");
        wrongHashResponse.Should().BeNull();
    }

    [Fact]
    public async Task EventStore_ConcurrencyControl_WithSqlServerDatabase()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = "TRADER001:AAPL";
        var correlationId = Guid.NewGuid().ToString();

        var event1 = new TestDomainEvent(aggregateId, "Trade", 1, correlationId, "System1");
        var event2 = new TestDomainEvent(aggregateId, "Trade", 2, correlationId, "System2");

        _mockSerializer.Setup(s => s.Serialize(It.IsAny<IDomainEvent>()))
            .ReturnsAsync(new SerializedEvent("TestDomainEvent", 1, "{\"test\": \"data\"}", "test-schema", DateTime.UtcNow, new Dictionary<string, string>()));

        await _repository.SaveEventsAsync(aggregateId, partitionKey, new[] { event1 }, 0);

        var act = async () => await _repository.SaveEventsAsync(aggregateId, partitionKey, new[] { event2 }, 0);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Concurrency conflict*");
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

        _context.EventStore.Add(entity1);
        await _context.SaveChangesAsync();

        _context.EventStore.Add(entity2);

        var act = async () => await _context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task EventStore_SqlServerTransactions_RollbackOnError()
    {
        var aggregateId = Guid.NewGuid().ToString();
        var partitionKey = "TRADER001:AAPL";
        var correlationId = Guid.NewGuid().ToString();
        var sharedEventId = Guid.NewGuid().ToString();

        var validEvent = new TestDomainEvent(aggregateId, "Trade", 1, correlationId, "TestSystem", sharedEventId);
        
        // Create an event with the same EventId to cause a unique constraint violation
        var duplicateEvent = new TestDomainEvent(aggregateId, "Trade", 2, correlationId, "TestSystem", sharedEventId);

        _mockSerializer.Setup(s => s.Serialize(It.IsAny<IDomainEvent>()))
            .ReturnsAsync(new SerializedEvent("TestDomainEvent", 1, "{\"test\": \"data\"}", "test-schema", DateTime.UtcNow, new Dictionary<string, string>()));

        var events = new List<IDomainEvent> { validEvent, duplicateEvent };

        var act = async () => await _repository.SaveEventsAsync(aggregateId, partitionKey, events, 0);

        await act.Should().ThrowAsync<Exception>();

        var savedEvents = await _context.EventStore.CountAsync();
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
            events.Add(new TestDomainEvent(aggregateId, "Trade", i, Guid.NewGuid().ToString(), "TestSystem"));
        }

        _mockSerializer.Setup(s => s.Serialize(It.IsAny<IDomainEvent>()))
            .ReturnsAsync((IDomainEvent e) => new SerializedEvent("TestDomainEvent", 1, $"{{\"AggregateVersion\":{e.AggregateVersion}}}", "test-schema", DateTime.UtcNow, new Dictionary<string, string>()));

        _mockSerializer.Setup(s => s.Deserialize(It.IsAny<SerializedEvent>()))
            .Returns((SerializedEvent se) => {
                // Extract version from serialized data and return corresponding event
                var versionMatch = System.Text.RegularExpressions.Regex.Match(se.Data, @"""AggregateVersion"":(\d+)");
                if (versionMatch.Success && int.TryParse(versionMatch.Groups[1].Value, out int version))
                {
                    return events.FirstOrDefault(e => e.AggregateVersion == version) ?? events.First();
                }
                return events.First();
            });

        var startTime = DateTime.UtcNow;
        
        foreach (var eventBatch in events.Chunk(10))
        {
            await _repository.SaveEventsAsync(aggregateId, partitionKey, eventBatch, eventBatch.First().AggregateVersion - 1);
        }

        var retrievedEvents = await _repository.GetEventsAsync(aggregateId);
        
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        retrievedEvents.Should().HaveCount(100);
        duration.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }
}