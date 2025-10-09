using Microsoft.Extensions.Logging;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Core.Common;
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

    public async Task<Result> SaveEventToOutboxAsync(IDomainEvent domainEvent, string topic, string partitionKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var serializationResult = await _serializationService.SerializeAsync(domainEvent);
            if (serializationResult.IsFailure)
            {
                _logger.LogError("Failed to serialize event {EventId}: {Error}", domainEvent.EventId, serializationResult.Error);
                return Result.Failure($"Failed to serialize event: {serializationResult.Error}");
            }

            var serializedEvent = serializationResult.Value!;
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

            var saveResult = await _outboxRepository.SaveOutboxEventAsync(outboxEvent, cancellationToken);
            if (saveResult.IsFailure)
                return saveResult;
            
            _logger.LogDebug("Saved event {EventId} to outbox for topic {Topic}", domainEvent.EventId, topic);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save event {EventId} to outbox", domainEvent.EventId);
            return Result.Failure($"Failed to save event to outbox: {ex.Message}");
        }
    }

    public async Task<Result> ProcessOutboxEventsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var unprocessedEventsResult = await _outboxRepository.GetUnprocessedEventsAsync(100, cancellationToken);
            if (unprocessedEventsResult.IsFailure)
                return Result.Failure(unprocessedEventsResult.Error);

            var unprocessedEvents = unprocessedEventsResult.Value!;
            
            foreach (var outboxEvent in unprocessedEvents)
            {
                try
                {
                    await _retryService.ExecuteWithRetryAsync(async () =>
                    {
                        var publishResult = await PublishEventAsync(outboxEvent, cancellationToken);
                        if (publishResult.IsFailure)
                            throw new InvalidOperationException(publishResult.Error);
                    }, maxRetries: 3, baseDelay: TimeSpan.FromMilliseconds(1), cancellationToken);
                    
                    var markProcessedResult = await _outboxRepository.MarkAsProcessedAsync(outboxEvent.Id, cancellationToken);
                    if (markProcessedResult.IsFailure)
                        throw new InvalidOperationException(markProcessedResult.Error);
                    
                    _logger.LogDebug("Successfully published outbox event {EventId} to topic {Topic}", 
                        outboxEvent.EventId, outboxEvent.Topic);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish outbox event {EventId} to topic {Topic} after retries", 
                        outboxEvent.EventId, outboxEvent.Topic);
                    
                    var deadLetterReason = $"Failed after retry attempts with exponential backoff. Last error: {ex.Message}";
                    var moveResult = await _outboxRepository.MoveToDeadLetterAsync(outboxEvent.Id, deadLetterReason, cancellationToken);
                    if (moveResult.IsFailure)
                        _logger.LogError("Failed to move event to dead letter: {Error}", moveResult.Error);
                    
                    _logger.LogError("Event {EventId} moved to dead letter queue after retry attempts. Reason: {Reason}", 
                        outboxEvent.EventId, deadLetterReason);
                }
            }
            
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing outbox events");
            return Result.Failure($"Error processing outbox events: {ex.Message}");
        }
    }

    public async Task<Result> RetryFailedEventsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var retryDelay = TimeSpan.FromMinutes(5); // Wait 5 minutes before retry
            var maxRetryCount = 3;
            
            var failedEventsResult = await _outboxRepository.GetFailedEventsForRetryAsync(retryDelay, maxRetryCount, cancellationToken);
            if (failedEventsResult.IsFailure)
                return Result.Failure(failedEventsResult.Error);

            var failedEvents = failedEventsResult.Value!;
            
            foreach (var outboxEvent in failedEvents)
            {
                var publishResult = await PublishEventAsync(outboxEvent, cancellationToken);
                if (publishResult.IsSuccess)
                {
                    var markProcessedResult = await _outboxRepository.MarkAsProcessedAsync(outboxEvent.Id, cancellationToken);
                    if (markProcessedResult.IsFailure)
                        return Result.Failure(markProcessedResult.Error);
                    
                    _logger.LogInformation("Successfully retried outbox event {EventId} to topic {Topic}", 
                        outboxEvent.EventId, outboxEvent.Topic);
                }
                else
                {
                    var incrementResult = await _outboxRepository.IncrementRetryCountAsync(outboxEvent.Id, cancellationToken);
                    if (incrementResult.IsFailure)
                        return Result.Failure(incrementResult.Error);
                    
                    var newRetryCount = outboxEvent.RetryCount + 1;
                    if (newRetryCount >= maxRetryCount)
                    {
                        var deadLetterReason = $"Exceeded max retry count ({maxRetryCount}). Last error: {publishResult.Error}";
                        var moveToDeadLetterResult = await _outboxRepository.MoveToDeadLetterAsync(outboxEvent.Id, deadLetterReason, cancellationToken);
                        if (moveToDeadLetterResult.IsFailure)
                            return Result.Failure(moveToDeadLetterResult.Error);
                        
                        _logger.LogError("Event {EventId} moved to dead letter queue after {RetryCount} failed attempts. Reason: {Reason}", 
                            outboxEvent.EventId, newRetryCount, deadLetterReason);
                    }
                    else
                    {
                        _logger.LogWarning("Retry failed for outbox event {EventId} to topic {Topic}. Retry count: {RetryCount}. Error: {Error}", 
                            outboxEvent.EventId, outboxEvent.Topic, newRetryCount, publishResult.Error);
                        
                        // Extract the original error message if it's wrapped
                        var errorMessage = publishResult.Error.StartsWith("Failed to publish event: ") 
                            ? publishResult.Error.Substring("Failed to publish event: ".Length)
                            : publishResult.Error;
                        
                        var markFailedResult = await _outboxRepository.MarkAsFailedAsync(outboxEvent.Id, errorMessage, cancellationToken);
                        if (markFailedResult.IsFailure)
                            return Result.Failure(markFailedResult.Error);
                    }
                }
            }
            
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying failed outbox events");
            return Result.Failure($"Error retrying failed outbox events: {ex.Message}");
        }
    }

    private async Task<Result> PublishEventAsync(OutboxEventEntity outboxEvent, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>
        {
            ["eventType"] = outboxEvent.EventType,
            ["eventId"] = outboxEvent.EventId,
            ["aggregateId"] = outboxEvent.AggregateId,
            ["aggregateType"] = outboxEvent.AggregateType,
            ["metadata"] = outboxEvent.Metadata
        };

        var result = await _kafkaProducer.ProduceAsync(
            outboxEvent.Topic,
            outboxEvent.PartitionKey,
            outboxEvent.EventData,
            headers,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure($"Failed to publish event: {result.Error}");
        }
        
        return Result.Success();
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

    public async Task<Result<IEnumerable<OutboxEventEntity>>> GetDeadLetteredEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var eventsResult = await _outboxRepository.GetDeadLetteredEventsAsync(batchSize, cancellationToken);
            if (eventsResult.IsFailure)
                return eventsResult;
            return Result<IEnumerable<OutboxEventEntity>>.Success(eventsResult.Value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dead lettered events");
            return Result<IEnumerable<OutboxEventEntity>>.Failure($"Error retrieving dead lettered events: {ex.Message}");
        }
    }

    public async Task<Result> ReprocessDeadLetteredEventAsync(long outboxEventId, CancellationToken cancellationToken = default)
    {
        try
        {
            var reprocessResult = await _outboxRepository.ReprocessDeadLetteredEventAsync(outboxEventId, cancellationToken);
            if (reprocessResult.IsFailure)
                return reprocessResult;
            _logger.LogInformation("Dead lettered event {EventId} has been reset for reprocessing", outboxEventId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reprocessing dead lettered event {EventId}", outboxEventId);
            return Result.Failure($"Error reprocessing dead lettered event {outboxEventId}: {ex.Message}");
        }
    }

    public async Task<Result<int>> GetDeadLetteredEventCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var countResult = await _outboxRepository.GetDeadLetteredEventCountAsync(cancellationToken);
            if (countResult.IsFailure)
                return countResult;
            return Result<int>.Success(countResult.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dead lettered event count");
            return Result<int>.Failure($"Error getting dead lettered event count: {ex.Message}");
        }
    }
}