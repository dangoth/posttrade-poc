using Microsoft.EntityFrameworkCore;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Entities;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PostTradeSystem.Infrastructure.Repositories;

public class EventStoreRepository : IEventStoreRepository
{
    private readonly PostTradeDbContext _context;
    private readonly SerializationManagementService _serializationService;

    public EventStoreRepository(PostTradeDbContext context, SerializationManagementService serializationService)
    {
        _context = context;
        _serializationService = serializationService;
    }

    public async Task<IEnumerable<IDomainEvent>> GetEventsAsync(string aggregateId, long fromVersion = 0, CancellationToken cancellationToken = default)
    {
        var eventEntities = await _context.EventStore
            .Where(e => e.AggregateId == aggregateId && e.AggregateVersion > fromVersion)
            .OrderBy(e => e.AggregateVersion)
            .ToListAsync(cancellationToken);

        var events = new List<IDomainEvent>();
        foreach (var entity in eventEntities)
        {
            var schemaVersion = ExtractSchemaVersionFromMetadata(entity.Metadata);
            var serializedEvent = new SerializedEvent(
                entity.EventType,
                schemaVersion,
                entity.EventData,
                "default",
                entity.OccurredAt,
                new Dictionary<string, string>());
            var domainEvent = _serializationService.Deserialize(serializedEvent);
            events.Add(domainEvent);
        }

        return events;
    }

    public async Task<IEnumerable<IDomainEvent>> GetEventsByPartitionKeyAsync(string partitionKey, long fromVersion = 0, CancellationToken cancellationToken = default)
    {
        var eventEntities = await _context.EventStore
            .Where(e => e.PartitionKey == partitionKey && e.AggregateVersion > fromVersion)
            .OrderBy(e => e.CreatedAt)
            .ThenBy(e => e.AggregateVersion)
            .ToListAsync(cancellationToken);

        var events = new List<IDomainEvent>();
        foreach (var entity in eventEntities)
        {
            var schemaVersion = ExtractSchemaVersionFromMetadata(entity.Metadata);
            var serializedEvent = new SerializedEvent(
                entity.EventType,
                schemaVersion,
                entity.EventData,
                "default",
                entity.OccurredAt,
                new Dictionary<string, string>());
            var domainEvent = _serializationService.Deserialize(serializedEvent);
            events.Add(domainEvent);
        }

        return events;
    }

    public async Task SaveEventsAsync(string aggregateId, string partitionKey, IEnumerable<IDomainEvent> events, long expectedVersion, CancellationToken cancellationToken = default)
    {
            var currentVersion = await GetCurrentVersionAsync(aggregateId, cancellationToken);
            
            if (currentVersion != expectedVersion)
            {
                throw new InvalidOperationException($"Concurrency conflict. Expected version {expectedVersion}, but current version is {currentVersion}");
            }

            var eventEntities = new List<EventStoreEntity>();
            
            foreach (var domainEvent in events)
            {
                var existingEvent = await _context.EventStore
                    .FirstOrDefaultAsync(e => e.EventId == domainEvent.EventId, cancellationToken);
                    
                if (existingEvent != null)
                {
                    continue;
                }

                var serializedEvent = await _serializationService.SerializeAsync(domainEvent);
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
                    EventType = eventType,  // Store "TradeCreated" instead of "TradeCreatedEvent"
                    EventData = eventData,
                    Metadata = metadata,
                    OccurredAt = domainEvent.OccurredAt,
                    CreatedAt = DateTime.UtcNow,
                    CorrelationId = domainEvent.CorrelationId,
                    CausedBy = domainEvent.CausedBy,
                    IsProcessed = false
                };

                eventEntities.Add(eventEntity);
            }

            if (eventEntities.Any())
            {
                _context.EventStore.AddRange(eventEntities);
                await _context.SaveChangesAsync(cancellationToken);
            }

    }

    public async Task<bool> CheckIdempotencyAsync(string idempotencyKey, string requestHash, CancellationToken cancellationToken = default)
    {
        var existing = await _context.IdempotencyKeys
            .FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey && i.RequestHash == requestHash, cancellationToken);

        return existing != null && existing.ExpiresAt > DateTime.UtcNow;
    }

    public async Task SaveIdempotencyAsync(string idempotencyKey, string aggregateId, string requestHash, string responseData, TimeSpan expiration, CancellationToken cancellationToken = default)
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

        _context.IdempotencyKeys.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetIdempotentResponseAsync(string idempotencyKey, string requestHash, CancellationToken cancellationToken = default)
    {
        var entity = await _context.IdempotencyKeys
            .FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey && i.RequestHash == requestHash, cancellationToken);

        if (entity == null || entity.ExpiresAt <= DateTime.UtcNow)
        {
            return null;
        }

        return entity.ResponseData;
    }

    public async Task MarkEventsAsProcessedAsync(IEnumerable<string> eventIds, CancellationToken cancellationToken = default)
    {
        var events = await _context.EventStore
            .Where(e => eventIds.Contains(e.EventId))
            .ToListAsync(cancellationToken);

        foreach (var eventEntity in events)
        {
            eventEntity.IsProcessed = true;
            eventEntity.ProcessedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<IDomainEvent>> GetUnprocessedEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var eventEntities = await _context.EventStore
            .Where(e => !e.IsProcessed)
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var events = new List<IDomainEvent>();
        foreach (var entity in eventEntities)
        {
            var schemaVersion = ExtractSchemaVersionFromMetadata(entity.Metadata);
            var serializedEvent = new SerializedEvent(
                entity.EventType,
                schemaVersion,
                entity.EventData,
                "default",
                entity.OccurredAt,
                new Dictionary<string, string>());
            var domainEvent = _serializationService.Deserialize(serializedEvent);
            events.Add(domainEvent);
        }

        return events;
    }

    private async Task<long> GetCurrentVersionAsync(string aggregateId, CancellationToken cancellationToken)
    {
        var lastEvent = await _context.EventStore
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

    private static int ExtractSchemaVersionFromMetadata(string metadata)
    {
        try
        {
            using var document = JsonDocument.Parse(metadata);
            if (document.RootElement.TryGetProperty("SchemaVersion", out var versionElement))
            {
                if (int.TryParse(versionElement.GetString(), out var version))
                {
                    return version;
                }
            }
        }
        catch (JsonException)
        {
            // If metadata is malformed, fall back to version 1
        }
        
        // Default to version 1 if not found or parsing fails
        return 1;
    }

    public async Task CleanupExpiredIdempotencyKeysAsync(CancellationToken cancellationToken = default)
    {
        var expiredKeys = await _context.IdempotencyKeys
            .Where(i => i.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        if (expiredKeys.Any())
        {
            _context.IdempotencyKeys.RemoveRange(expiredKeys);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes);
    }
}