using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Common;

namespace PostTradeSystem.Infrastructure.Repositories;

public interface IEventStoreRepository
{
    Task<Result<IEnumerable<IDomainEvent>>> GetEventsAsync(string aggregateId, long fromVersion = 0, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<IDomainEvent>>> GetEventsByPartitionKeyAsync(string partitionKey, long fromVersion = 0, CancellationToken cancellationToken = default);
    Task<Result> SaveEventsAsync(string aggregateId, string partitionKey, IEnumerable<IDomainEvent> events, long expectedVersion, CancellationToken cancellationToken = default);
    Task<Result<bool>> CheckIdempotencyAsync(string idempotencyKey, string requestHash, CancellationToken cancellationToken = default);
    Task<Result> SaveIdempotencyAsync(string idempotencyKey, string aggregateId, string requestHash, string responseData, TimeSpan expiration, CancellationToken cancellationToken = default);
    Task<Result<string?>> GetIdempotentResponseAsync(string idempotencyKey, string requestHash, CancellationToken cancellationToken = default);
    Task<Result> MarkEventsAsProcessedAsync(IEnumerable<string> eventIds, CancellationToken cancellationToken = default);
    Task<Result> CleanupExpiredIdempotencyKeysAsync(CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<IDomainEvent>>> GetUnprocessedEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    
    // Temporal query capabilities for analytics
    Task<Result<IEnumerable<IDomainEvent>>> GetEventsByTimeRangeAsync(DateTime fromTime, DateTime toTime, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<IDomainEvent>>> GetEventsByAggregateTypeAsync(string aggregateType, DateTime? fromTime = null, DateTime? toTime = null, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<IDomainEvent>>> GetAllEventsInChronologicalOrderAsync(DateTime? fromTime = null, DateTime? toTime = null, int? limit = null, CancellationToken cancellationToken = default);
    
    // Event replay capabilities
    Task<Result<IEnumerable<IDomainEvent>>> GetEventsForReplayAsync(string aggregateId, long fromVersion = 0, long? toVersion = null, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<IDomainEvent>>> GetEventsByVersionRangeAsync(string aggregateId, long fromVersion, long toVersion, CancellationToken cancellationToken = default);
}