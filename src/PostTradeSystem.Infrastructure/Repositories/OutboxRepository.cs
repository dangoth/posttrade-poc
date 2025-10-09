using Microsoft.EntityFrameworkCore;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Core.Common;
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

    public async Task<Result> SaveOutboxEventAsync(OutboxEventEntity outboxEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            OutboxEvents.Add(outboxEvent);
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to save outbox event: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<OutboxEventEntity>>> GetUnprocessedEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var events = await OutboxEvents
                .Where(e => !e.IsProcessed && !e.IsDeadLettered)
                .OrderBy(e => e.CreatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
            return Result<IEnumerable<OutboxEventEntity>>.Success(events);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<OutboxEventEntity>>.Failure($"Failed to get unprocessed events: {ex.Message}");
        }
    }

    public async Task<Result> MarkAsProcessedAsync(long outboxEventId, CancellationToken cancellationToken = default)
    {
        try
        {
            var outboxEvent = await OutboxEvents
                .FirstOrDefaultAsync(e => e.Id == outboxEventId, cancellationToken);

            if (outboxEvent != null)
            {
                outboxEvent.IsProcessed = true;
                outboxEvent.ProcessedAt = _timeProvider.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to mark event as processed: {ex.Message}");
        }
    }

    public async Task<Result> MarkAsFailedAsync(long outboxEventId, string errorMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            var outboxEvent = await OutboxEvents
                .FirstOrDefaultAsync(e => e.Id == outboxEventId, cancellationToken);

            if (outboxEvent != null)
            {
                outboxEvent.ErrorMessage = errorMessage;
                outboxEvent.LastRetryAt = _timeProvider.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to mark event as failed: {ex.Message}");
        }
    }

    public async Task<Result> IncrementRetryCountAsync(long outboxEventId, CancellationToken cancellationToken = default)
    {
        try
        {
            var outboxEvent = await OutboxEvents
                .FirstOrDefaultAsync(e => e.Id == outboxEventId, cancellationToken);

            if (outboxEvent != null)
            {
                outboxEvent.RetryCount++;
                outboxEvent.LastRetryAt = _timeProvider.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to increment retry count: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<OutboxEventEntity>>> GetFailedEventsForRetryAsync(TimeSpan retryDelay, int maxRetryCount = 3, CancellationToken cancellationToken = default)
    {
        try
        {
            var retryThreshold = _timeProvider.UtcNow.Subtract(retryDelay);
            
            var events = await OutboxEvents
                .Where(e => !e.IsProcessed 
                    && !e.IsDeadLettered
                    && e.RetryCount < maxRetryCount 
                    && (e.LastRetryAt == null || e.LastRetryAt <= retryThreshold))
                .OrderBy(e => e.CreatedAt)
                .ToListAsync(cancellationToken);
            return Result<IEnumerable<OutboxEventEntity>>.Success(events);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<OutboxEventEntity>>.Failure($"Failed to get failed events for retry: {ex.Message}");
        }
    }

    public async Task<Result> MoveToDeadLetterAsync(long outboxEventId, string reason, CancellationToken cancellationToken = default)
    {
        try
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
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to move event to dead letter: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<OutboxEventEntity>>> GetDeadLetteredEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var events = await OutboxEvents
                .Where(e => e.IsDeadLettered)
                .OrderBy(e => e.DeadLetteredAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
            return Result<IEnumerable<OutboxEventEntity>>.Success(events);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<OutboxEventEntity>>.Failure($"Failed to get dead lettered events: {ex.Message}");
        }
    }

    public async Task<Result> ReprocessDeadLetteredEventAsync(long outboxEventId, CancellationToken cancellationToken = default)
    {
        try
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
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to reprocess dead lettered event: {ex.Message}");
        }
    }

    public async Task<Result<int>> GetDeadLetteredEventCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await OutboxEvents
                .CountAsync(e => e.IsDeadLettered, cancellationToken);
            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Failed to get dead lettered event count: {ex.Message}");
        }
    }

    public async Task<Result<OutboxEventEntity?>> GetOutboxEventByIdAsync(long outboxEventId, CancellationToken cancellationToken = default)
    {
        try
        {
            var outboxEvent = await OutboxEvents
                .FirstOrDefaultAsync(e => e.Id == outboxEventId, cancellationToken);
            return Result<OutboxEventEntity?>.Success(outboxEvent);
        }
        catch (Exception ex)
        {
            return Result<OutboxEventEntity?>.Failure($"Failed to get outbox event by ID: {ex.Message}");
        }
    }
}