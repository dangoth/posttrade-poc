using PostTradeSystem.Core.Aggregates;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Common;
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

    public async Task<Result<T?>> GetByIdAsync(string aggregateId, CancellationToken cancellationToken = default)
    {
        try
        {
            var eventsResult = await _eventStoreRepository.GetEventsAsync(aggregateId, 0, cancellationToken);
            if (eventsResult.IsFailure)
                return Result<T?>.Failure(eventsResult.Error);

            var events = eventsResult.Value!;
            if (!events.Any())
            {
                return Result<T?>.Success(null);
            }

            var firstEvent = events.First();
            var partitionKey = GeneratePartitionKey(firstEvent);
            
            var aggregate = _aggregateFactory(aggregateId, partitionKey, events);
            return Result<T?>.Success(aggregate);
        }
        catch (Exception ex)
        {
            return Result<T?>.Failure($"Failed to get aggregate by ID: {ex.Message}");
        }
    }

    public async Task<Result> SaveAsync(T aggregate, CancellationToken cancellationToken = default)
    {
        try
        {
            var uncommittedEvents = aggregate.GetUncommittedEvents();
            
            if (!uncommittedEvents.Any())
            {
                return Result.Success();
            }

            var expectedVersion = uncommittedEvents.First().AggregateVersion - 1;
            
            var saveResult = await _eventStoreRepository.SaveEventsAsync(
                aggregate.Id,
                aggregate.PartitionKey,
                uncommittedEvents,
                expectedVersion,
                cancellationToken);

            if (saveResult.IsFailure)
                return saveResult;

            aggregate.MarkEventsAsCommitted();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to save aggregate: {ex.Message}");
        }
    }

    public async Task<Result> SaveAsync(T aggregate, string idempotencyKey, string requestHash, CancellationToken cancellationToken = default)
    {
        try
        {
            var idempotencyResult = await _eventStoreRepository.CheckIdempotencyAsync(idempotencyKey, requestHash, cancellationToken);
            if (idempotencyResult.IsFailure)
                return Result.Failure(idempotencyResult.Error);

            if (idempotencyResult.Value)
            {
                return Result.Success();
            }

            var saveResult = await SaveAsync(aggregate, cancellationToken);
            if (saveResult.IsFailure)
                return saveResult;

            var responseData = JsonSerializer.Serialize(new { AggregateId = aggregate.Id, Success = true });
            var idempotencySaveResult = await _eventStoreRepository.SaveIdempotencyAsync(
                idempotencyKey,
                aggregate.Id,
                requestHash,
                responseData,
                TimeSpan.FromHours(24),
                cancellationToken);

            return idempotencySaveResult;
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to save aggregate with idempotency: {ex.Message}");
        }
    }

    public async Task<Result<T?>> GetByIdAtVersionAsync(string aggregateId, long version, CancellationToken cancellationToken = default)
    {
        try
        {
            var eventsResult = await _eventStoreRepository.GetEventsByVersionRangeAsync(aggregateId, 1, version, cancellationToken);
            if (eventsResult.IsFailure)
                return Result<T?>.Failure(eventsResult.Error);

            var events = eventsResult.Value!;
            if (!events.Any())
            {
                return Result<T?>.Success(null);
            }

            var firstEvent = events.First();
            var partitionKey = GeneratePartitionKey(firstEvent);
            
            var aggregate = _aggregateFactory(aggregateId, partitionKey, events);
            return Result<T?>.Success(aggregate);
        }
        catch (Exception ex)
        {
            return Result<T?>.Failure($"Failed to get aggregate by ID at version {version}: {ex.Message}");
        }
    }

    public async Task<Result<T?>> GetByIdAtTimeAsync(string aggregateId, DateTime pointInTime, CancellationToken cancellationToken = default)
    {
        try
        {
            var eventsResult = await _eventStoreRepository.GetEventsAsync(aggregateId, 0, cancellationToken);
            if (eventsResult.IsFailure)
                return Result<T?>.Failure(eventsResult.Error);

            var allEvents = eventsResult.Value!;
            var eventsUpToTime = allEvents.Where(e => e.OccurredAt <= pointInTime).ToList();
            
            if (!eventsUpToTime.Any())
            {
                return Result<T?>.Success(null);
            }

            var firstEvent = eventsUpToTime.First();
            var partitionKey = GeneratePartitionKey(firstEvent);
            
            var aggregate = _aggregateFactory(aggregateId, partitionKey, eventsUpToTime);
            return Result<T?>.Success(aggregate);
        }
        catch (Exception ex)
        {
            return Result<T?>.Failure($"Failed to get aggregate by ID at time {pointInTime}: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<T>>> ReplayAggregatesFromEventsAsync(IEnumerable<string> aggregateIds, long fromVersion = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            var aggregates = new List<T>();
            
            foreach (var aggregateId in aggregateIds)
            {
                var eventsResult = await _eventStoreRepository.GetEventsAsync(aggregateId, fromVersion, cancellationToken);
                if (eventsResult.IsFailure)
                    return Result<IEnumerable<T>>.Failure(eventsResult.Error);

                var events = eventsResult.Value!;
                if (events.Any())
                {
                    var firstEvent = events.First();
                    var partitionKey = GeneratePartitionKey(firstEvent);
                    var aggregate = _aggregateFactory(aggregateId, partitionKey, events);
                    aggregates.Add(aggregate);
                }
            }

            return Result<IEnumerable<T>>.Success(aggregates);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<T>>.Failure($"Failed to replay aggregates from events: {ex.Message}");
        }
    }

    private static string GeneratePartitionKey(IDomainEvent domainEvent)
    {
        return $"{domainEvent.AggregateType}:{domainEvent.AggregateId}";
    }
}