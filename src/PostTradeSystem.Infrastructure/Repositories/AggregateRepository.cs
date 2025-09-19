using PostTradeSystem.Core.Aggregates;
using PostTradeSystem.Core.Events;
using System.Text.Json;

namespace PostTradeSystem.Infrastructure.Repositories;

public class AggregateRepository<T> : IAggregateRepository<T> where T : class, IAggregateRoot
{
    private readonly IEventStoreRepository _eventStoreRepository;
    private readonly Func<string, string, IEnumerable<IDomainEvent>, T> _aggregateFactory;

    public AggregateRepository(
        IEventStoreRepository eventStoreRepository,
        Func<string, string, IEnumerable<IDomainEvent>, T> aggregateFactory)
    {
        _eventStoreRepository = eventStoreRepository;
        _aggregateFactory = aggregateFactory;
    }

    public async Task<T?> GetByIdAsync(string aggregateId, CancellationToken cancellationToken = default)
    {
        var events = await _eventStoreRepository.GetEventsAsync(aggregateId, 0, cancellationToken);
        
        if (!events.Any())
        {
            return null;
        }

        var firstEvent = events.First();
        var partitionKey = GeneratePartitionKey(firstEvent);
        
        return _aggregateFactory(aggregateId, partitionKey, events);
    }

    public async Task SaveAsync(T aggregate, CancellationToken cancellationToken = default)
    {
        var uncommittedEvents = aggregate.GetUncommittedEvents();
        
        if (!uncommittedEvents.Any())
        {
            return;
        }

        var expectedVersion = uncommittedEvents.First().AggregateVersion - 1;
        
        await _eventStoreRepository.SaveEventsAsync(
            aggregate.Id,
            aggregate.PartitionKey,
            uncommittedEvents,
            expectedVersion,
            cancellationToken);

        aggregate.MarkEventsAsCommitted();
    }

    public async Task SaveAsync(T aggregate, string idempotencyKey, string requestHash, CancellationToken cancellationToken = default)
    {
        var isIdempotent = await _eventStoreRepository.CheckIdempotencyAsync(idempotencyKey, requestHash, cancellationToken);
        
        if (isIdempotent)
        {
            return;
        }

        await SaveAsync(aggregate, cancellationToken);

        var responseData = JsonSerializer.Serialize(new { AggregateId = aggregate.Id, Success = true });
        await _eventStoreRepository.SaveIdempotencyAsync(
            idempotencyKey,
            aggregate.Id,
            requestHash,
            responseData,
            TimeSpan.FromHours(24),
            cancellationToken);
    }

    private static string GeneratePartitionKey(IDomainEvent domainEvent)
    {
        return $"{domainEvent.AggregateType}:{domainEvent.AggregateId}";
    }
}