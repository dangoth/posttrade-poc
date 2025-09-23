using PostTradeSystem.Core.Events;

namespace PostTradeSystem.Infrastructure.Repositories;

public interface IEventStoreRepository
{
    Task<IEnumerable<IDomainEvent>> GetEventsAsync(string aggregateId, long fromVersion = 0, CancellationToken cancellationToken = default);
    Task<IEnumerable<IDomainEvent>> GetEventsByPartitionKeyAsync(string partitionKey, long fromVersion = 0, CancellationToken cancellationToken = default);
    Task SaveEventsAsync(string aggregateId, string partitionKey, IEnumerable<IDomainEvent> events, long expectedVersion, CancellationToken cancellationToken = default);
    Task<bool> CheckIdempotencyAsync(string idempotencyKey, string requestHash, CancellationToken cancellationToken = default);
    Task SaveIdempotencyAsync(string idempotencyKey, string aggregateId, string requestHash, string responseData, TimeSpan expiration, CancellationToken cancellationToken = default);
    Task<string?> GetIdempotentResponseAsync(string idempotencyKey, string requestHash, CancellationToken cancellationToken = default);
    Task MarkEventsAsProcessedAsync(IEnumerable<string> eventIds, CancellationToken cancellationToken = default);
    Task CleanupExpiredIdempotencyKeysAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<IDomainEvent>> GetUnprocessedEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default);
}