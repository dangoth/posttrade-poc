using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Helpers;
using PostTradeSystem.Core.Messages;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Infrastructure.Configuration;
using PostTradeSystem.Infrastructure.Health;
using PostTradeSystem.Infrastructure.Repositories;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PostTradeSystem.Infrastructure.Kafka;

public class KafkaConsumerService : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly KafkaHealthService _healthService;
    private readonly IJsonSchemaValidator _schemaValidator;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
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

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { 
                new JsonStringEnumConverter(),
                new DictionaryObjectJsonConverter()
            }
        };

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

            var (envelope, tradeEvent) = await DeserializeAndCreateEvent(messageType, message.Value, correlationId);
            
            if (envelope == null || tradeEvent == null)
            {
                _logger.LogError("Failed to create trade event from {MessageType} message", messageType);
                return false;
            }

            var partitionKey = CreatePartitionKey(envelope, messageType);
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

            var messageId = GetMessageId(envelope);
            _logger.LogInformation("Successfully processed {MessageType} message {MessageId} from {Topic}:{Partition}:{Offset}", 
                messageType, messageId, consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);
            
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

    private async Task<(object? envelope, TradeCreatedEvent? tradeEvent)> DeserializeAndCreateEvent(string messageType, string messageValue, string correlationId)
    {
        try
        {
            return messageType.ToUpperInvariant() switch
            {
                "EQUITY" => await ProcessEquityMessage(messageValue, correlationId),
                "FX" => await ProcessFxMessage(messageValue, correlationId),
                "OPTION" => await ProcessOptionMessage(messageValue, correlationId),
                _ => (null, null)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing {MessageType} message", messageType);
            return (null, null);
        }
    }

    private Task<(TradeMessageEnvelope<EquityTradeMessage>? envelope, TradeCreatedEvent? tradeEvent)> ProcessEquityMessage(string messageValue, string correlationId)
    {
        var envelope = JsonSerializer.Deserialize<TradeMessageEnvelope<EquityTradeMessage>>(messageValue, _jsonOptions);
        if (envelope?.Payload == null) return Task.FromResult<(TradeMessageEnvelope<EquityTradeMessage>?, TradeCreatedEvent?)>((null, null));

        var additionalData = new Dictionary<string, object>
        {
            ["Symbol"] = envelope.Payload.Symbol ?? string.Empty,
            ["Exchange"] = envelope.Payload.Exchange ?? string.Empty,
            ["Sector"] = envelope.Payload.Sector ?? string.Empty,
            ["DividendRate"] = envelope.Payload.DividendRate,
            ["Isin"] = envelope.Payload.Isin ?? string.Empty,
            ["MarketSegment"] = envelope.Payload.MarketSegment ?? string.Empty,
            ["SourceSystem"] = envelope.Payload.SourceSystem ?? string.Empty
        };

        var tradeEvent = new TradeCreatedEvent(
            envelope.Payload.TradeId,
            envelope.Payload.TraderId,
            envelope.Payload.InstrumentId,
            envelope.Payload.Quantity,
            envelope.Payload.Price,
            envelope.Payload.Direction,
            envelope.Payload.TradeDateTime,
            envelope.Payload.Currency,
            envelope.Payload.CounterpartyId,
            "EQUITY",
            1,
            correlationId,
            "KafkaConsumerService",
            additionalData);

        return Task.FromResult<(TradeMessageEnvelope<EquityTradeMessage>?, TradeCreatedEvent?)>((envelope, tradeEvent));
    }

    private Task<(TradeMessageEnvelope<FxTradeMessage>? envelope, TradeCreatedEvent? tradeEvent)> ProcessFxMessage(string messageValue, string correlationId)
    {
        var envelope = JsonSerializer.Deserialize<TradeMessageEnvelope<FxTradeMessage>>(messageValue, _jsonOptions);
        if (envelope?.Payload == null) return Task.FromResult<(TradeMessageEnvelope<FxTradeMessage>?, TradeCreatedEvent?)>((null, null));

        var additionalData = new Dictionary<string, object>
        {
            ["BaseCurrency"] = envelope.Payload.BaseCurrency ?? string.Empty,
            ["QuoteCurrency"] = envelope.Payload.QuoteCurrency ?? string.Empty,
            ["SettlementDate"] = envelope.Payload.SettlementDate,
            ["SpotRate"] = envelope.Payload.SpotRate,
            ["ForwardPoints"] = envelope.Payload.ForwardPoints,
            ["TradeType"] = envelope.Payload.TradeType ?? string.Empty,
            ["DeliveryMethod"] = envelope.Payload.DeliveryMethod ?? string.Empty,
            ["SourceSystem"] = envelope.Payload.SourceSystem ?? string.Empty
        };

        var tradeEvent = new TradeCreatedEvent(
            envelope.Payload.TradeId,
            envelope.Payload.TraderId,
            envelope.Payload.InstrumentId,
            envelope.Payload.Quantity,
            envelope.Payload.Price,
            envelope.Payload.Direction,
            envelope.Payload.TradeDateTime,
            envelope.Payload.Currency,
            envelope.Payload.CounterpartyId,
            "FX",
            1,
            correlationId,
            "KafkaConsumerService",
            additionalData);

        return Task.FromResult<(TradeMessageEnvelope<FxTradeMessage>?, TradeCreatedEvent?)>((envelope, tradeEvent));
    }

    private Task<(TradeMessageEnvelope<OptionTradeMessage>? envelope, TradeCreatedEvent? tradeEvent)> ProcessOptionMessage(string messageValue, string correlationId)
    {
        var envelope = JsonSerializer.Deserialize<TradeMessageEnvelope<OptionTradeMessage>>(messageValue, _jsonOptions);
        if (envelope?.Payload == null) return Task.FromResult<(TradeMessageEnvelope<OptionTradeMessage>?, TradeCreatedEvent?)>((null, null));

        var additionalData = new Dictionary<string, object>
        {
            ["UnderlyingSymbol"] = envelope.Payload.UnderlyingSymbol ?? string.Empty,
            ["StrikePrice"] = envelope.Payload.StrikePrice,
            ["ExpirationDate"] = envelope.Payload.ExpirationDate,
            ["OptionType"] = envelope.Payload.OptionType ?? string.Empty,
            ["Exchange"] = envelope.Payload.Exchange ?? string.Empty,
            ["ImpliedVolatility"] = envelope.Payload.ImpliedVolatility,
            ["ContractSize"] = envelope.Payload.ContractSize ?? string.Empty,
            ["SettlementType"] = envelope.Payload.SettlementType ?? string.Empty,
            ["SourceSystem"] = envelope.Payload.SourceSystem ?? string.Empty
        };

        var tradeEvent = new TradeCreatedEvent(
            envelope.Payload.TradeId,
            envelope.Payload.TraderId,
            envelope.Payload.InstrumentId,
            envelope.Payload.Quantity,
            envelope.Payload.Price,
            envelope.Payload.Direction,
            envelope.Payload.TradeDateTime,
            envelope.Payload.Currency,
            envelope.Payload.CounterpartyId,
            "OPTION",
            1,
            correlationId,
            "KafkaConsumerService",
            additionalData);

        return Task.FromResult<(TradeMessageEnvelope<OptionTradeMessage>?, TradeCreatedEvent?)>((envelope, tradeEvent));
    }

    private string CreatePartitionKey(object envelope, string messageType)
    {
        var envelopeType = envelope.GetType();
        if (envelopeType.IsGenericType && envelopeType.GetGenericTypeDefinition() == typeof(TradeMessageEnvelope<>))
        {
            var payloadProperty = envelopeType.GetProperty("Payload");
            if (payloadProperty?.GetValue(envelope) is TradeMessage payload)
            {
                return payload.GetPartitionKey();
            }
        }
        
        return $"unknown:{messageType}";
    }

    private string GetMessageId(object envelope)
    {
        var envelopeType = envelope.GetType();
        if (envelopeType.IsGenericType && envelopeType.GetGenericTypeDefinition() == typeof(TradeMessageEnvelope<>))
        {
            var messageIdProperty = envelopeType.GetProperty("MessageId");
            if (messageIdProperty?.GetValue(envelope) is string messageId)
            {
                return messageId;
            }
        }
        
        return "Unknown";
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}