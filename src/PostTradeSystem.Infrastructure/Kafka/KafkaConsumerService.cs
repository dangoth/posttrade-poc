using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostTradeSystem.Core.Adapters;
using PostTradeSystem.Core.Routing;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Infrastructure.Configuration;
using PostTradeSystem.Infrastructure.Health;
using PostTradeSystem.Infrastructure.Repositories;
using System.Text.Json;

namespace PostTradeSystem.Infrastructure.Kafka;

public class KafkaConsumerService : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly KafkaHealthService _healthService;
    private readonly IJsonSchemaValidator _schemaValidator;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly string[] _topics;
    private readonly KafkaExactlyOnceConfiguration _exactlyOnceConfig;

    public KafkaConsumerService(
        IConfiguration configuration, 
        IServiceScopeFactory serviceScopeFactory,
        KafkaHealthService healthService,
        IJsonSchemaValidator schemaValidator,
        ILogger<KafkaConsumerService> logger,
        IOptions<KafkaExactlyOnceConfiguration> exactlyOnceOptions)
    {
        _logger = logger;
        _healthService = healthService;
        _serviceScopeFactory = serviceScopeFactory;
        _schemaValidator = schemaValidator;
        _exactlyOnceConfig = exactlyOnceOptions.Value;
        
        var kafkaBootstrapServers = configuration.GetSection("Kafka:BootstrapServers").Value ?? 
                                   configuration.GetConnectionString("Kafka") ?? 
                                   "localhost:9092";
        
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaBootstrapServers,
            GroupId = _exactlyOnceConfig.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnablePartitionEof = false,
            SessionTimeoutMs = _exactlyOnceConfig.ConsumerSessionTimeoutMs,
            HeartbeatIntervalMs = _exactlyOnceConfig.ConsumerHeartbeatIntervalMs,
            MaxPollIntervalMs = _exactlyOnceConfig.ConsumerMaxPollIntervalMs
        };

        if (_exactlyOnceConfig.EnableExactlyOnceSemantics)
        {
            config.IsolationLevel = IsolationLevel.ReadCommitted;
            config.EnableAutoOffsetStore = !_exactlyOnceConfig.EnableManualOffsetManagement;
        }

        _consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => logger.LogError("Kafka consumer error: {Error}", e.Reason))
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                logger.LogInformation("Assigned partitions: [{Partitions}]", 
                    string.Join(", ", partitions.Select(p => $"{p.Topic}:{p.Partition}")));
                
                foreach (var partition in partitions)
                {
                    logger.LogDebug("Consumer group rebalance: Assigned partition {Topic}:{Partition}", 
                        partition.Topic, partition.Partition);
                }
                
                _healthService.SetHealthy($"Consumer assigned {partitions.Count} partitions");
            })
            .SetPartitionsRevokedHandler((c, partitions) =>
            {
                logger.LogInformation("Revoked partitions: [{Partitions}]", 
                    string.Join(", ", partitions.Select(p => $"{p.Topic}:{p.Partition}")));
                
                try
                {
                    c.Commit(partitions);
                    logger.LogDebug("Successfully committed offsets before partition revocation");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to commit offsets during partition revocation");
                }
                
                _healthService.SetDegraded($"Consumer lost {partitions.Count} partitions during rebalance");
            })
            .SetPartitionsLostHandler((c, partitions) =>
            {
                logger.LogWarning("Lost partitions: [{Partitions}]", 
                    string.Join(", ", partitions.Select(p => $"{p.Topic}:{p.Partition}")));
                
                _healthService.SetDegraded($"Consumer lost {partitions.Count} partitions unexpectedly");
            })
            .Build();

        _topics = new[] { "trades.equities", "trades.options", "trades.fx" };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting Kafka consumer...");
                    _healthService.SetDegraded("Kafka consumer starting...");
                    
                    await Task.Run(() => _consumer.Subscribe(_topics), stoppingToken);
                    _logger.LogInformation("Kafka consumer subscribed to topics: {Topics}", string.Join(", ", _topics));
                    _healthService.SetHealthy("Kafka consumer connected and subscribed");

                    await ConsumeMessagesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Kafka consumer shutdown requested");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Kafka consumer failed: {Message}", ex.Message);
                    _healthService.SetDegraded($"Kafka consumer failed: {ex.Message}");
                    
                    _logger.LogInformation("Retrying Kafka consumer in 30 seconds...");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            _healthService.SetDegraded("Kafka consumer stopped");
            
            try
            {
                _consumer.Close();
                _logger.LogInformation("Kafka consumer stopped");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing Kafka consumer");
            }
        }, stoppingToken);

        await Task.CompletedTask;
    }

    private async Task ConsumeMessagesAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));
                
                if (consumeResult?.Message != null)
                {
                    var success = await ProcessMessageAsync(consumeResult);
                    if (success)
                    {
                        if (_exactlyOnceConfig.EnableExactlyOnceSemantics && _exactlyOnceConfig.EnableManualOffsetManagement)
                        {
                            _consumer.StoreOffset(consumeResult);
                        }
                        _consumer.Commit(consumeResult);
                    }
                }
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Error consuming message: {Error}", ex.Error.Reason);
                _healthService.SetDegraded($"Kafka consume error: {ex.Error.Reason}");
               
                if (ex.Error.IsFatal)
                {
                    _logger.LogError("Fatal Kafka error, will restart consumer");
                    throw;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in consumer loop");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessMessageAsync(ConsumeResult<string, string> consumeResult)
    {
        var message = consumeResult.Message;
        var messageTypeHeader = message.Headers?.FirstOrDefault(h => h.Key == "messageType");
        
        if (messageTypeHeader == null)
        {
            _logger.LogWarning("Message without messageType header received from {Topic}:{Partition}:{Offset}", 
                consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);
            return false;
        }

        var messageType = System.Text.Encoding.UTF8.GetString(messageTypeHeader.GetValueBytes());
        var correlationIdHeader = message.Headers?.FirstOrDefault(h => h.Key == "correlationId");
        var correlationId = correlationIdHeader != null ? 
            System.Text.Encoding.UTF8.GetString(correlationIdHeader.GetValueBytes()) : 
            Guid.NewGuid().ToString();

        try
        {
            var schemaValidationResult = ValidateMessageSchema(_schemaValidator, messageType, message.Value);
            if (!schemaValidationResult.IsValid)
            {
                _logger.LogError("Schema validation failed for {MessageType} from {Topic}:{Partition}:{Offset}. Error: {Error}", 
                    messageType, consumeResult.Topic, consumeResult.Partition, consumeResult.Offset, 
                    schemaValidationResult.ErrorMessage);
                return false;
            }

            var messageKey = $"{consumeResult.Topic}:{consumeResult.Partition}:{consumeResult.Offset}";
            var requestHash = EventStoreRepository.ComputeHash(message.Value);
            
            using var scope = _serviceScopeFactory.CreateScope();
            var eventStoreRepository = scope.ServiceProvider.GetRequiredService<IEventStoreRepository>();
            
            var idempotencyResult = await eventStoreRepository.CheckIdempotencyAsync(messageKey, requestHash);
            if (idempotencyResult.IsFailure)
            {
                _logger.LogError("Failed to check idempotency for key {MessageKey}: {Error}", messageKey, idempotencyResult.Error);
                return false;
            }
            
            idempotencyResult = await eventStoreRepository.CheckIdempotencyAsync(messageKey, requestHash);
            if (idempotencyResult.IsSuccess && idempotencyResult.Value)
            {
                _logger.LogInformation("Duplicate message detected, skipping: {MessageKey}", messageKey);
                return true;
            }
            else if (idempotencyResult.IsFailure)
            {
                _logger.LogError("Failed to check idempotency for key {MessageKey}: {Error}", messageKey, idempotencyResult.Error);
                throw new InvalidOperationException($"Idempotency check failed: {idempotencyResult.Error}");
            }
            var adapterFactory = scope.ServiceProvider.GetRequiredService<ITradeMessageAdapterFactory>();
            var messageRouter = scope.ServiceProvider.GetRequiredService<IMessageRouter>();
            
            var sourceSystem = messageRouter.DetermineSourceSystem(consumeResult.Topic, 
                message.Headers?.ToDictionary(h => h.Key, h => System.Text.Encoding.UTF8.GetString(h.GetValueBytes())));
            
            if (!adapterFactory.CanProcessMessage(messageType, sourceSystem))
            {
                _logger.LogWarning("No adapter available for message type {MessageType} from source system {SourceSystem}", 
                    messageType, sourceSystem);
                return false;
            }
            
            var tradeEvent = await adapterFactory.ProcessMessageAsync(messageType, sourceSystem, message.Value, correlationId);
            
            if (tradeEvent == null)
            {
                _logger.LogError("Failed to create trade event from {MessageType} message", messageType);
                return false;
            }

            var partitionKey = messageRouter.GetPartitionKey(messageType, sourceSystem, tradeEvent.TraderId, tradeEvent.InstrumentId);
            await eventStoreRepository.SaveEventsAsync(
                tradeEvent.AggregateId, 
                partitionKey, 
                new[] { tradeEvent }, 
                0);

            await eventStoreRepository.SaveIdempotencyAsync(
                messageKey, 
                tradeEvent.AggregateId, 
                requestHash, 
                "processed", 
                TimeSpan.FromHours(24));

            _logger.LogInformation("Successfully processed {MessageType} message {TradeId} from {Topic}:{Partition}:{Offset}", 
                messageType, tradeEvent.AggregateId, consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);
            
            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize message from {Topic}:{Partition}:{Offset}", 
                consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);
            return false;
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Unsupported message type {MessageType} from {Topic}:{Partition}:{Offset}", 
                messageType, consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from {Topic}:{Partition}:{Offset}", 
                consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);
            return false;
        }
    }

    private ValidationResult ValidateMessageSchema(IJsonSchemaValidator schemaValidator, string messageType, string messageValue)
    {
        try
        {
            var schemaKey = messageType.ToUpperInvariant() switch
            {
                "EQUITY" => "EquityTradeMessage",
                "FX" => "FxTradeMessage", 
                "OPTION" => "OptionTradeMessage",
                _ => "TradeMessage"
            };

            var isValid = schemaValidator.ValidateMessage(schemaKey, messageValue, 1);
            return new ValidationResult(isValid, isValid ? null : $"Schema validation failed for {schemaKey}");
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Schema validation error: {ex.Message}");
        }
    }


    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}