using PostTradeSystem.Core.Aggregates;
using PostTradeSystem.Core.Common;

namespace PostTradeSystem.Infrastructure.Repositories;

public interface IAggregateRepository<T> where T : class, IAggregateRoot
{
    Task<Result<T?>> GetByIdAsync(string aggregateId, CancellationToken cancellationToken = default);
    Task<Result> SaveAsync(T aggregate, CancellationToken cancellationToken = default);
    Task<Result> SaveAsync(T aggregate, string idempotencyKey, string requestHash, CancellationToken cancellationToken = default);
}