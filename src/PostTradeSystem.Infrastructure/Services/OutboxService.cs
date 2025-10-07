using Microsoft.Extensions.Logging;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Infrastructure.Entities;
using PostTradeSystem.Infrastructure.Kafka;
using PostTradeSystem.Infrastructure.Repositories;
using System.Text.Json;

namespace PostTradeSystem.Infrastructure.Services;

public class OutboxService : IOutboxService
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly IKafkaProducerService _kafkaProducer;
    private readonly ISerializationManagementService _serializationService;
    private readonly IRetryService _retryService;
    private readonly ILogger<OutboxService> _logger;
    private readonly ITimeProvider _timeProvider;

    public OutboxService(
        IOutboxRepository outboxRepository,
        IKafkaProducerService kafkaProducer,
        ISerializationManagementService serializationService,
        IRetryService retryService,
        ILogger<OutboxService> logger,
        ITimeProvider? timeProvider = null)
    {
        _outboxRepository = outboxRepository;
        _kafkaProducer = kafkaProducer;
        _serializationService = serializationService;
        _retryService = retryService;
        _logger = logger;
        _timeProvider = timeProvider ?? new SystemTimeProvider();
    }

    public async Task SaveEventToOutboxAsync(IDomainEvent domainEvent, string topic, string partitionKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var serializedEvent = await _serializationService.SerializeAsync(domainEvent);
            var metadata = CreateMetadata(domainEvent, serializedEvent);

            var eventTypeName = domainEvent.GetType().Name;
            var eventType = eventTypeName.EndsWith("Event") ? eventTypeName[..^5] : eventTypeName;

            var outboxEvent = new OutboxEventEntity
            {
                EventId = domainEvent.EventId,
                AggregateId = domainEvent.AggregateId,
                AggregateType = domainEvent.AggregateType,
                EventType = eventType,
                EventData = serializedEvent.Data,
                Metadata = metadata,
                Topic = topic,
                PartitionKey = partitionKey,
                CreatedAt = DateTime.UtcNow,
                IsProcessed = false,
                RetryCount = 0
            };

            await _outboxRepository.SaveOutboxEventAsync(outboxEvent, cancellationToken);
            
            _logger.LogDebug("Saved event {EventId} to outbox for topic {Topic}", domainEvent.EventId, topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save event {EventId} to outbox", domainEvent.EventId);
            throw;
        }
    }

    public async Task ProcessOutboxEventsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var unprocessedEvents = await _outboxRepository.GetUnprocessedEventsAsync(100, cancellationToken);
            
            foreach (var outboxEvent in unprocessedEvents)
            {
                try
                {
                    await _retryService.ExecuteWithRetryAsync(async () =>
                    {
                        await PublishEventAsync(outboxEvent, cancellationToken);
                    }, maxRetries: 3, baseDelay: TimeSpan.FromMilliseconds(1), cancellationToken);
                    
                    await _outboxRepository.MarkAsProcessedAsync(outboxEvent.Id, cancellationToken);
                    
                    _logger.LogDebug("Successfully published outbox event {EventId} to topic {Topic}", 
                        outboxEvent.EventId, outboxEvent.Topic);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish outbox event {EventId} to topic {Topic} after retries", 
                        outboxEvent.EventId, outboxEvent.Topic);
                    
                    var deadLetterReason = $"Failed after retry attempts with exponential backoff. Last error: {ex.Message}";
                    await _outboxRepository.MoveToDeadLetterAsync(outboxEvent.Id, deadLetterReason, cancellationToken);
                    
                    _logger.LogError("Event {EventId} moved to dead letter queue after retry attempts. Reason: {Reason}", 
                        outboxEvent.EventId, deadLetterReason);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing outbox events");
            throw;
        }
    }

    public async Task RetryFailedEventsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var retryDelay = TimeSpan.FromMinutes(5); // Wait 5 minutes before retry
            var maxRetryCount = 3;
            
            var failedEvents = await _outboxRepository.GetFailedEventsForRetryAsync(retryDelay, maxRetryCount, cancellationToken);
            
            foreach (var outboxEvent in failedEvents)
            {
                try
                {
                    await PublishEventAsync(outboxEvent, cancellationToken);
                    await _outboxRepository.MarkAsProcessedAsync(outboxEvent.Id, cancellationToken);
                    
                    _logger.LogInformation("Successfully retried outbox event {EventId} to topic {Topic}", 
                        outboxEvent.EventId, outboxEvent.Topic);
                }
                catch (Exception ex)
                {
                    await _outboxRepository.IncrementRetryCountAsync(outboxEvent.Id, cancellationToken);
                    
                    var newRetryCount = outboxEvent.RetryCount + 1;
                    if (newRetryCount >= maxRetryCount)
                    {
                        var deadLetterReason = $"Exceeded max retry count ({maxRetryCount}). Last error: {ex.Message}";
                        await _outboxRepository.MoveToDeadLetterAsync(outboxEvent.Id, deadLetterReason, cancellationToken);
                        
                        _logger.LogError("Event {EventId} moved to dead letter queue after {RetryCount} failed attempts. Reason: {Reason}", 
                            outboxEvent.EventId, newRetryCount, deadLetterReason);
                    }
                    else
                    {
                        _logger.LogWarning(ex, "Retry failed for outbox event {EventId} to topic {Topic}. Retry count: {RetryCount}", 
                            outboxEvent.EventId, outboxEvent.Topic, newRetryCount);
                        
                        await _outboxRepository.MarkAsFailedAsync(outboxEvent.Id, ex.Message, cancellationToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying failed outbox events");
            throw;
        }
    }

    private async Task PublishEventAsync(OutboxEventEntity outboxEvent, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>
        {
            ["eventType"] = outboxEvent.EventType,
            ["eventId"] = outboxEvent.EventId,
            ["aggregateId"] = outboxEvent.AggregateId,
            ["aggregateType"] = outboxEvent.AggregateType,
            ["metadata"] = outboxEvent.Metadata
        };

        await _kafkaProducer.ProduceAsync(
            outboxEvent.Topic,
            outboxEvent.PartitionKey,
            outboxEvent.EventData,
            headers,
            cancellationToken);
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
            SerializedAt = serializedEvent.SerializedAt,
            OutboxCreatedAt = DateTime.UtcNow
        };

        return JsonSerializer.Serialize(metadata);
    }

    public async Task<IEnumerable<OutboxEventEntity>> GetDeadLetteredEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _outboxRepository.GetDeadLetteredEventsAsync(batchSize, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dead lettered events");
            throw;
        }
    }

    public async Task ReprocessDeadLetteredEventAsync(long outboxEventId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _outboxRepository.ReprocessDeadLetteredEventAsync(outboxEventId, cancellationToken);
            _logger.LogInformation("Dead lettered event {EventId} has been reset for reprocessing", outboxEventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reprocessing dead lettered event {EventId}", outboxEventId);
            throw;
        }
    }

    public async Task<int> GetDeadLetteredEventCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _outboxRepository.GetDeadLetteredEventCountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dead lettered event count");
            throw;
        }
    }
}