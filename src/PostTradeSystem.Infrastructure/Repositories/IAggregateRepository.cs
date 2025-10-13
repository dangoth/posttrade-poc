using PostTradeSystem.Core.Aggregates;
using PostTradeSystem.Core.Common;

namespace PostTradeSystem.Infrastructure.Repositories;

public interface IAggregateRepository<T> where T : class, IAggregateRoot
{
    Task<Result<T?>> GetByIdAsync(string aggregateId, CancellationToken cancellationToken = default);
    Task<Result> SaveAsync(T aggregate, CancellationToken cancellationToken = default);
    Task<Result> SaveAsync(T aggregate, string idempotencyKey, string requestHash, CancellationToken cancellationToken = default);
    
    // Temporal reconstruction capabilities
    Task<Result<T?>> GetByIdAtVersionAsync(string aggregateId, long version, CancellationToken cancellationToken = default);
    Task<Result<T?>> GetByIdAtTimeAsync(string aggregateId, DateTime pointInTime, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<T>>> ReplayAggregatesFromEventsAsync(IEnumerable<string> aggregateIds, long fromVersion = 0, CancellationToken cancellationToken = default);
}