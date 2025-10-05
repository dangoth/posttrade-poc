using PostTradeSystem.Core.Events;
using PostTradeSystem.Infrastructure.Entities;

namespace PostTradeSystem.Infrastructure.Services;

public interface IOutboxService
{
    Task SaveEventToOutboxAsync(IDomainEvent domainEvent, string topic, string partitionKey, CancellationToken cancellationToken = default);
    Task ProcessOutboxEventsAsync(CancellationToken cancellationToken = default);
    Task RetryFailedEventsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<OutboxEventEntity>> GetDeadLetteredEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    Task ReprocessDeadLetteredEventAsync(long outboxEventId, CancellationToken cancellationToken = default);
    Task<int> GetDeadLetteredEventCountAsync(CancellationToken cancellationToken = default);
}