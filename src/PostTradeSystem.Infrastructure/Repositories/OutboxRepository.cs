using Microsoft.EntityFrameworkCore;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Entities;

namespace PostTradeSystem.Infrastructure.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly PostTradeDbContext _context;
    private readonly ITimeProvider _timeProvider;

    public OutboxRepository(PostTradeDbContext context, ITimeProvider? timeProvider = null)
    {
        _context = context;
        _timeProvider = timeProvider ?? new SystemTimeProvider();
    }

    private DbSet<OutboxEventEntity> OutboxEvents => _context.Set<OutboxEventEntity>();

    public async Task SaveOutboxEventAsync(OutboxEventEntity outboxEvent, CancellationToken cancellationToken = default)
    {
        OutboxEvents.Add(outboxEvent);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<OutboxEventEntity>> GetUnprocessedEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        return await OutboxEvents
            .Where(e => !e.IsProcessed && !e.IsDeadLettered)
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkAsProcessedAsync(long outboxEventId, CancellationToken cancellationToken = default)
    {
        var outboxEvent = await OutboxEvents
            .FirstOrDefaultAsync(e => e.Id == outboxEventId, cancellationToken);

        if (outboxEvent != null)
        {
            outboxEvent.IsProcessed = true;
            outboxEvent.ProcessedAt = _timeProvider.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsFailedAsync(long outboxEventId, string errorMessage, CancellationToken cancellationToken = default)
    {
        var outboxEvent = await OutboxEvents
            .FirstOrDefaultAsync(e => e.Id == outboxEventId, cancellationToken);

        if (outboxEvent != null)
        {
            outboxEvent.ErrorMessage = errorMessage;
            outboxEvent.LastRetryAt = _timeProvider.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task IncrementRetryCountAsync(long outboxEventId, CancellationToken cancellationToken = default)
    {
        var outboxEvent = await OutboxEvents
            .FirstOrDefaultAsync(e => e.Id == outboxEventId, cancellationToken);

        if (outboxEvent != null)
        {
            outboxEvent.RetryCount++;
            outboxEvent.LastRetryAt = _timeProvider.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<OutboxEventEntity>> GetFailedEventsForRetryAsync(TimeSpan retryDelay, int maxRetryCount = 3, CancellationToken cancellationToken = default)
    {
        var retryThreshold = _timeProvider.UtcNow.Subtract(retryDelay);
        
        return await OutboxEvents
            .Where(e => !e.IsProcessed 
                && !e.IsDeadLettered
                && e.RetryCount < maxRetryCount 
                && (e.LastRetryAt == null || e.LastRetryAt <= retryThreshold))
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task MoveToDeadLetterAsync(long outboxEventId, string reason, CancellationToken cancellationToken = default)
    {
        var outboxEvent = await OutboxEvents
            .FirstOrDefaultAsync(e => e.Id == outboxEventId, cancellationToken);

        if (outboxEvent != null)
        {
            outboxEvent.IsDeadLettered = true;
            outboxEvent.DeadLetteredAt = _timeProvider.UtcNow;
            outboxEvent.DeadLetterReason = reason;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<OutboxEventEntity>> GetDeadLetteredEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        return await OutboxEvents
            .Where(e => e.IsDeadLettered)
            .OrderBy(e => e.DeadLetteredAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task ReprocessDeadLetteredEventAsync(long outboxEventId, CancellationToken cancellationToken = default)
    {
        var outboxEvent = await OutboxEvents
            .FirstOrDefaultAsync(e => e.Id == outboxEventId && e.IsDeadLettered, cancellationToken);

        if (outboxEvent != null)
        {
            outboxEvent.IsDeadLettered = false;
            outboxEvent.IsProcessed = false;
            outboxEvent.ProcessedAt = null;
            outboxEvent.DeadLetteredAt = null;
            outboxEvent.DeadLetterReason = null;
            outboxEvent.RetryCount = 0;
            outboxEvent.LastRetryAt = null;
            outboxEvent.ErrorMessage = null;
            
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> GetDeadLetteredEventCountAsync(CancellationToken cancellationToken = default)
    {
        return await OutboxEvents
            .CountAsync(e => e.IsDeadLettered, cancellationToken);
    }

    public async Task<OutboxEventEntity?> GetOutboxEventByIdAsync(long outboxEventId, CancellationToken cancellationToken = default)
    {
        return await OutboxEvents
            .FirstOrDefaultAsync(e => e.Id == outboxEventId, cancellationToken);
    }
}