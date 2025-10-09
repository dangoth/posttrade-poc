using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Common;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Entities;
using PostTradeSystem.Infrastructure.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PostTradeSystem.Infrastructure.Repositories;

public class EventStoreRepository : IEventStoreRepository
{
    private readonly PostTradeDbContext _context;
    private readonly ISerializationManagementService _serializationService;
    private readonly IOutboxService? _outboxService;
    private readonly ILogger<EventStoreRepository> _logger;

    public EventStoreRepository(
        PostTradeDbContext context, 
        ISerializationManagementService serializationService,
        ILogger<EventStoreRepository> logger,
        IOutboxService? outboxService = null)
    {
        _context = context;
        _serializationService = serializationService;
        _logger = logger;
        _outboxService = outboxService;
    }

    private DbSet<EventStoreEntity> EventStore => _context.Set<EventStoreEntity>();
    private DbSet<IdempotencyEntity> IdempotencyKeys => _context.Set<IdempotencyEntity>();

    public async Task<Result<IEnumerable<IDomainEvent>>> GetEventsAsync(string aggregateId, long fromVersion = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            var eventEntities = await EventStore
                .Where(e => e.AggregateId == aggregateId && e.AggregateVersion > fromVersion)
                .OrderBy(e => e.AggregateVersion)
                .ToListAsync(cancellationToken);

            var events = new List<IDomainEvent>();
            foreach (var entity in eventEntities)
            {
                var metadataResult = ExtractSchemaVersionFromMetadata(entity.Metadata, entity.EventId, entity.AggregateId, entity.OccurredAt);
                
                if (metadataResult.ShouldDeadLetter)
                {
                    _logger.LogError("Skipping event {EventId} due to metadata parsing failure: {Reason}", 
                        entity.EventId, metadataResult.DeadLetterReason);
                    
                    if (_outboxService != null)
                    {
                        await MoveEventToDeadLetterAsync(entity, metadataResult.DeadLetterReason!);
                    }
                    continue;
                }

                if (!metadataResult.IsReliable)
                {
                    _logger.LogWarning("Using unreliable schema version {Version} for event {EventId}: {Warning}", 
                        metadataResult.SchemaVersion, entity.EventId, metadataResult.WarningMessage);
                }

                var serializedEvent = new SerializedEvent(
                    entity.EventType,
                    metadataResult.SchemaVersion,
                    entity.EventData,
                    "default",
                    entity.OccurredAt,
                    new Dictionary<string, string>());
                    
                var deserializeResult = _serializationService.Deserialize(serializedEvent);
                if (deserializeResult.IsFailure)
                {
                    _logger.LogError("Failed to deserialize event {EventId} with schema version {Version}: {Error}", 
                        entity.EventId, metadataResult.SchemaVersion, deserializeResult.Error);
                    
                    if (_outboxService != null)
                    {
                        await MoveEventToDeadLetterAsync(entity, $"Deserialization failed: {deserializeResult.Error}");
                    }
                    continue;
                }
                
                events.Add(deserializeResult.Value!);
            }

            return Result<IEnumerable<IDomainEvent>>.Success(events);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<IDomainEvent>>.Failure($"Failed to get events: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<IDomainEvent>>> GetEventsByPartitionKeyAsync(string partitionKey, long fromVersion = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            var eventEntities = await EventStore
                .Where(e => e.PartitionKey == partitionKey && e.AggregateVersion > fromVersion)
                .OrderBy(e => e.CreatedAt)
                .ThenBy(e => e.AggregateVersion)
                .ToListAsync(cancellationToken);

            var events = new List<IDomainEvent>();
            foreach (var entity in eventEntities)
            {
                var metadataResult = ExtractSchemaVersionFromMetadata(entity.Metadata, entity.EventId, entity.AggregateId, entity.OccurredAt);
                
                if (metadataResult.ShouldDeadLetter)
                {
                    _logger.LogError("Skipping event {EventId} due to metadata parsing failure: {Reason}", 
                        entity.EventId, metadataResult.DeadLetterReason);
                    
                    if (_outboxService != null)
                    {
                        await MoveEventToDeadLetterAsync(entity, metadataResult.DeadLetterReason!);
                    }
                    continue;
                }
            
            var serializedEvent = new SerializedEvent(
                entity.EventType,
                metadataResult.SchemaVersion,
                entity.EventData,
                "default",
                entity.OccurredAt,
                new Dictionary<string, string>());
                
            var deserializeResult = _serializationService.Deserialize(serializedEvent);
            if (deserializeResult.IsFailure)
            {
                if (_outboxService != null)
                {
                    var moveResult = await MoveEventToDeadLetterAsync(entity, $"Deserialization failed: {deserializeResult.Error}");
                    if (moveResult.IsFailure)
                    {
                        _logger.LogError("Failed to move event to dead letter: {Error}", moveResult.Error);
                    }
                }
                continue;
            }
            
            events.Add(deserializeResult.Value!);
        }

        return Result<IEnumerable<IDomainEvent>>.Success(events);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<IDomainEvent>>.Failure($"Failed to get events by partition key: {ex.Message}");
        }
    }

    public async Task<Result> SaveEventsAsync(string aggregateId, string partitionKey, IEnumerable<IDomainEvent> events, long expectedVersion, CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            var currentVersion = await GetCurrentVersionAsync(aggregateId, cancellationToken);
            
            if (currentVersion != expectedVersion)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure($"Concurrency conflict. Expected version {expectedVersion}, but current version is {currentVersion}");
            }

            var eventEntities = new List<EventStoreEntity>();
            var eventsToPublish = new List<IDomainEvent>();
            
            foreach (var domainEvent in events)
            {
                var existingEvent = await EventStore
                    .FirstOrDefaultAsync(e => e.EventId == domainEvent.EventId, cancellationToken);
                    
                if (existingEvent != null)
                {
                    continue;
                }

                var serializeResult = await _serializationService.SerializeAsync(domainEvent);
                if (serializeResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure($"Failed to serialize event {domainEvent.EventId}: {serializeResult.Error}");
                }
                
                var serializedEvent = serializeResult.Value!;
                var eventData = serializedEvent.Data;
                var metadata = CreateMetadata(domainEvent, serializedEvent);

                // Use the same event type name conversion as the serialization service
                var eventTypeName = domainEvent.GetType().Name;
                var eventType = eventTypeName.EndsWith("Event") ? eventTypeName[..^5] : eventTypeName;

                var eventEntity = new EventStoreEntity
                {
                    EventId = domainEvent.EventId,
                    AggregateId = domainEvent.AggregateId,
                    AggregateType = domainEvent.AggregateType,
                    PartitionKey = partitionKey,
                    AggregateVersion = domainEvent.AggregateVersion,
                    EventType = eventType,
                    EventData = eventData,
                    Metadata = metadata,
                    OccurredAt = domainEvent.OccurredAt,
                    CreatedAt = DateTime.UtcNow,
                    CorrelationId = domainEvent.CorrelationId,
                    CausedBy = domainEvent.CausedBy,
                    IsProcessed = false
                };

                eventEntities.Add(eventEntity);
                eventsToPublish.Add(domainEvent);
            }

            if (eventEntities.Any())
            {
                EventStore.AddRange(eventEntities);
                await _context.SaveChangesAsync(cancellationToken);

                if (_outboxService != null)
                {
                    foreach (var domainEvent in eventsToPublish)
                    {
                        var topic = DetermineTopicForEvent(domainEvent);
                        var outboxResult = await _outboxService.SaveEventToOutboxAsync(domainEvent, topic, partitionKey, cancellationToken);
                        if (outboxResult.IsFailure)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return Result.Failure($"Failed to save event to outbox: {outboxResult.Error}");
                        }
                    }
                }

                // Commit the transaction - both event store and outbox writes succeed or fail together
                await transaction.CommitAsync(cancellationToken);
                return Result.Success();
            }
            else
            {
                await transaction.CommitAsync(cancellationToken);
                return Result.Success();
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure($"Failed to save events: {ex.Message}");
        }
    }

    public async Task<Result<bool>> CheckIdempotencyAsync(string idempotencyKey, string requestHash, CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await IdempotencyKeys
                .FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey && i.RequestHash == requestHash, cancellationToken);

            var result = existing != null && existing.ExpiresAt > DateTime.UtcNow;
            return Result<bool>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure($"Failed to check idempotency: {ex.Message}");
        }
    }

    public async Task<Result> SaveIdempotencyAsync(string idempotencyKey, string aggregateId, string requestHash, string responseData, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = new IdempotencyEntity
            {
                IdempotencyKey = idempotencyKey,
                AggregateId = aggregateId,
                RequestHash = requestHash,
                ResponseData = responseData,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(expiration)
            };

            IdempotencyKeys.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to save idempotency: {ex.Message}");
        }
    }

    public async Task<Result<string?>> GetIdempotentResponseAsync(string idempotencyKey, string requestHash, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await IdempotencyKeys
                .FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey && i.RequestHash == requestHash, cancellationToken);

            if (entity == null || entity.ExpiresAt <= DateTime.UtcNow)
            {
                return Result<string?>.Success(null);
            }

            return Result<string?>.Success(entity.ResponseData);
        }
        catch (Exception ex)
        {
            return Result<string?>.Failure($"Failed to get idempotent response: {ex.Message}");
        }
    }

    public async Task<Result> MarkEventsAsProcessedAsync(IEnumerable<string> eventIds, CancellationToken cancellationToken = default)
    {
        try
        {
            var events = await EventStore
                .Where(e => eventIds.Contains(e.EventId))
                .ToListAsync(cancellationToken);

            foreach (var eventEntity in events)
            {
                eventEntity.IsProcessed = true;
                eventEntity.ProcessedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to mark events as processed: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<IDomainEvent>>> GetUnprocessedEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var eventEntities = await EventStore
                .Where(e => !e.IsProcessed)
                .OrderBy(e => e.CreatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            var events = new List<IDomainEvent>();
            foreach (var entity in eventEntities)
            {
                var metadataResult = ExtractSchemaVersionFromMetadata(entity.Metadata, entity.EventId, entity.AggregateId, entity.OccurredAt);
                if (metadataResult.ShouldDeadLetter)
                {
                    if (_outboxService != null)
                    {
                        var moveResult = await MoveEventToDeadLetterAsync(entity, metadataResult.DeadLetterReason!);
                        if (moveResult.IsFailure)
                        {
                            _logger.LogError("Failed to move event to dead letter: {Error}", moveResult.Error);
                        }
                    }
                    continue;
                }
                var serializedEvent = new SerializedEvent(
                    entity.EventType,
                    metadataResult.SchemaVersion,
                    entity.EventData,
                    "default",
                    entity.OccurredAt,
                    new Dictionary<string, string>());
                    
                var deserializeResult = _serializationService.Deserialize(serializedEvent);
                if (deserializeResult.IsFailure)
                {
                    if (_outboxService != null)
                    {
                        await MoveEventToDeadLetterAsync(entity, $"Deserialization failed: {deserializeResult.Error}");
                    }
                    continue;
                }
                
                events.Add(deserializeResult.Value!);
            }

            return Result<IEnumerable<IDomainEvent>>.Success(events);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<IDomainEvent>>.Failure($"Failed to get unprocessed events: {ex.Message}");
        }
    }

    private async Task<long> GetCurrentVersionAsync(string aggregateId, CancellationToken cancellationToken)
    {
        var lastEvent = await EventStore
            .Where(e => e.AggregateId == aggregateId)
            .OrderByDescending(e => e.AggregateVersion)
            .FirstOrDefaultAsync(cancellationToken);

        return lastEvent?.AggregateVersion ?? 0;
    }

    private static string CreateMetadata(IDomainEvent domainEvent, SerializedEvent serializedEvent)
    {
        var metadata = new
        {
            SchemaVersion = serializedEvent.SchemaVersion.ToString(),
            MetadataVersion = "1.0",
            SchemaId = serializedEvent.SchemaId,
            EventSource = "PostTradeSystem",
            CreatedBy = domainEvent.CausedBy,
            CorrelationId = domainEvent.CorrelationId,
            SerializedAt = serializedEvent.SerializedAt
        };

        return JsonSerializer.Serialize(metadata);
    }

    private MetadataParsingResult ExtractSchemaVersionFromMetadata(string metadata, string eventId, string aggregateId, DateTime eventOccurredAt)
    {
        // Strategy 1: Try to parse explicit SchemaVersion from metadata
        try
        {
            using var document = JsonDocument.Parse(metadata);
            if (document.RootElement.TryGetProperty("SchemaVersion", out var versionElement))
            {
                if (int.TryParse(versionElement.GetString(), out var version) && version > 0)
                {
                    _logger.LogDebug("Successfully extracted schema version {Version} from metadata for event {EventId}", 
                        version, eventId);
                    return MetadataParsingResult.Success(version, MetadataParsingStrategy.ExplicitVersion);
                }
                else
                {
                    _logger.LogWarning("Invalid schema version in metadata for event {EventId}, aggregate {AggregateId}: {Value}", 
                        eventId, aggregateId, versionElement.GetString());
                    // Fall through to Strategy 2
                }
            }
            else
            {
                _logger.LogWarning("Missing SchemaVersion in metadata for event {EventId}, aggregate {AggregateId}", 
                    eventId, aggregateId);
                // Fall through to Strategy 2
            }
        }
        catch (JsonException ex)
        {
            var deadLetterReason = $"Malformed JSON metadata: {ex.Message}";
            _logger.LogError(ex, "Failed to parse metadata JSON for event {EventId}, aggregate {AggregateId}. Moving to dead letter queue", 
                eventId, aggregateId);
            return MetadataParsingResult.DeadLetter(deadLetterReason);
        }

        // Strategy 2: Historical fallback - events before a certain date are likely V1
        // This is a safer assumption than blindly defaulting to V1
        var historicalCutoff = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc); // Adjust based on your deployment history
        var eventCreationTime = eventOccurredAt;
        
        if (eventCreationTime < historicalCutoff)
        {
            var warning = $"Using historical fallback to V1 for event created before {historicalCutoff:yyyy-MM-dd}";
            _logger.LogWarning("Using historical fallback for event {EventId}, aggregate {AggregateId}: {Warning}", 
                eventId, aggregateId, warning);
            return MetadataParsingResult.Warning(1, MetadataParsingStrategy.HistoricalFallback, warning);
        }

        // Strategy 3: Dead letter for recent events with unparseable metadata
        var finalDeadLetterReason = "Cannot determine schema version for recent event with malformed metadata";
        _logger.LogError("Cannot determine schema version for event {EventId}, aggregate {AggregateId}. Moving to dead letter queue: {Reason}", 
            eventId, aggregateId, finalDeadLetterReason);
        return MetadataParsingResult.DeadLetter(finalDeadLetterReason);
    }

    private async Task<Result> MoveEventToDeadLetterAsync(EventStoreEntity entity, string reason)
    {
        try
        {
            // Create a dead letter event that contains the original event data
            var deadLetterEvent = new DeadLetterEvent(
                entity.AggregateId,
                entity.EventType,
                entity.EventData,
                entity.Metadata,
                reason,
                entity.AggregateVersion,
                Guid.NewGuid().ToString(),
                "EventStoreRepository");

            var saveResult = await _outboxService!.SaveEventToOutboxAsync(deadLetterEvent, "events.deadletter", entity.AggregateId);
            if (saveResult.IsFailure)
            {
                _logger.LogError("Failed to save dead letter event to outbox: {Error}", saveResult.Error);
                return Result.Failure($"Failed to save dead letter event: {saveResult.Error}");
            }
            
            _logger.LogInformation("Successfully moved event {EventId} to dead letter queue with reason: {Reason}", 
                entity.EventId, reason);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move event {EventId} to dead letter queue", entity.EventId);
            return Result.Failure($"Failed to move event to dead letter queue: {ex.Message}");
        }
    }

    public async Task<Result> CleanupExpiredIdempotencyKeysAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var expiredKeys = await IdempotencyKeys
                .Where(i => i.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync(cancellationToken);

            if (expiredKeys.Any())
            {
                IdempotencyKeys.RemoveRange(expiredKeys);
                await _context.SaveChangesAsync(cancellationToken);
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to cleanup expired idempotency keys: {ex.Message}");
        }
    }

    private static string DetermineTopicForEvent(IDomainEvent domainEvent)
    {
        // Route events to appropriate topics based on event type or aggregate type
        return domainEvent.AggregateType.ToLowerInvariant() switch
        {
            "trade" => "events.trades",
            _ => "events.general"
        };
    }

    public static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes);
    }
}