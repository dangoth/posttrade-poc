using PostTradeSystem.Infrastructure.Entities;
using PostTradeSystem.Core.Common;

namespace PostTradeSystem.Infrastructure.Repositories;

public interface IOutboxRepository
{
    Task<Result> SaveOutboxEventAsync(OutboxEventEntity outboxEvent, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<OutboxEventEntity>>> GetUnprocessedEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    Task<Result> MarkAsProcessedAsync(long outboxEventId, CancellationToken cancellationToken = default);
    Task<Result> MarkAsFailedAsync(long outboxEventId, string errorMessage, CancellationToken cancellationToken = default);
    Task<Result> IncrementRetryCountAsync(long outboxEventId, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<OutboxEventEntity>>> GetFailedEventsForRetryAsync(TimeSpan retryDelay, int maxRetryCount = 3, CancellationToken cancellationToken = default);
    Task<Result> MoveToDeadLetterAsync(long outboxEventId, string reason, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<OutboxEventEntity>>> GetDeadLetteredEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    Task<Result> ReprocessDeadLetteredEventAsync(long outboxEventId, CancellationToken cancellationToken = default);
    Task<Result<int>> GetDeadLetteredEventCountAsync(CancellationToken cancellationToken = default);
    Task<Result<OutboxEventEntity?>> GetOutboxEventByIdAsync(long outboxEventId, CancellationToken cancellationToken = default);
}