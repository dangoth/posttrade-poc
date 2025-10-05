using PostTradeSystem.Infrastructure.Entities;

namespace PostTradeSystem.Infrastructure.Repositories;

public interface IOutboxRepository
{
    Task SaveOutboxEventAsync(OutboxEventEntity outboxEvent, CancellationToken cancellationToken = default);
    Task<IEnumerable<OutboxEventEntity>> GetUnprocessedEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    Task MarkAsProcessedAsync(long outboxEventId, CancellationToken cancellationToken = default);
    Task MarkAsFailedAsync(long outboxEventId, string errorMessage, CancellationToken cancellationToken = default);
    Task IncrementRetryCountAsync(long outboxEventId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OutboxEventEntity>> GetFailedEventsForRetryAsync(TimeSpan retryDelay, int maxRetryCount = 3, CancellationToken cancellationToken = default);
    Task MoveToDeadLetterAsync(long outboxEventId, string reason, CancellationToken cancellationToken = default);
    Task<IEnumerable<OutboxEventEntity>> GetDeadLetteredEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    Task ReprocessDeadLetteredEventAsync(long outboxEventId, CancellationToken cancellationToken = default);
    Task<int> GetDeadLetteredEventCountAsync(CancellationToken cancellationToken = default);
    Task<OutboxEventEntity?> GetOutboxEventByIdAsync(long outboxEventId, CancellationToken cancellationToken = default);
}