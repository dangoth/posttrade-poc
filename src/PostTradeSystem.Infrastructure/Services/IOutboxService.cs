using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Common;
using PostTradeSystem.Infrastructure.Entities;

namespace PostTradeSystem.Infrastructure.Services;

public interface IOutboxService
{
    Task<Result> SaveEventToOutboxAsync(IDomainEvent domainEvent, string topic, string partitionKey, CancellationToken cancellationToken = default);
    Task<Result> ProcessOutboxEventsAsync(CancellationToken cancellationToken = default);
    Task<Result> RetryFailedEventsAsync(CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<OutboxEventEntity>>> GetDeadLetteredEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    Task<Result> ReprocessDeadLetteredEventAsync(long outboxEventId, CancellationToken cancellationToken = default);
    Task<Result<int>> GetDeadLetteredEventCountAsync(CancellationToken cancellationToken = default);
}