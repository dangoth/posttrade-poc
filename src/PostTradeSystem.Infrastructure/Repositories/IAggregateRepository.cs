using PostTradeSystem.Core.Aggregates;

namespace PostTradeSystem.Infrastructure.Repositories;

public interface IAggregateRepository<T> where T : class, IAggregateRoot
{
    Task<T?> GetByIdAsync(string aggregateId, CancellationToken cancellationToken = default);
    Task SaveAsync(T aggregate, CancellationToken cancellationToken = default);
    Task SaveAsync(T aggregate, string idempotencyKey, string requestHash, CancellationToken cancellationToken = default);
}