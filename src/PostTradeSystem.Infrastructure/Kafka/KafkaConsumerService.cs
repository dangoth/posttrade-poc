using Confluent.Kafka;
using PostTradeSystem.Core.Messages;
using PostTradeSystem.Core.Serialization;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PostTradeSystem.Infrastructure.Health;

namespace PostTradeSystem.Infrastructure.Kafka;

public class KafkaConsumerService : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly SerializationManagementService _serializationService;
    private readonly KafkaHealthService _healthService;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string[] _topics;

    public KafkaConsumerService(
        IConfiguration configuration, 
        SerializationManagementService serializationService,
        KafkaHealthService healthService,
        ILogger<KafkaConsumerService> logger)
    {
        _logger = logger;
        _healthService = healthService;
        _serializationService = serializationService;
        
        var kafkaBootstrapServers = configuration.GetSection("Kafka:BootstrapServers").Value ?? 
                                   configuration.GetConnectionString("Kafka") ?? 
                                   "localhost:9092";
        
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaBootstrapServers,
            GroupId = "posttrade-consumer-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnablePartitionEof = false,
            SessionTimeoutMs = 30000,
            HeartbeatIntervalMs = 10000,
            MaxPollIntervalMs = 300000
        };

        _consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => logger.LogError("Kafka consumer error: {Error}", e.Reason))
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                logger.LogInformation("Assigned partitions: [{Partitions}]", 
                    string.Join(", ", partitions.Select(p => $"{p.Topic}:{p.Partition}")));
            })
            .SetPartitionsRevokedHandler((c, partitions) =>
            {
                logger.LogInformation("Revoked partitions: [{Partitions}]", 
                    string.Join(", ", partitions.Select(p => $"{p.Topic}:{p.Partition}")));
            })
            .Build();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
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
                    await ProcessMessageAsync(consumeResult);
                    _consumer.Commit(consumeResult);
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

    private Task ProcessMessageAsync(ConsumeResult<string, string> consumeResult)
    {
        var message = consumeResult.Message;
        var messageTypeHeader = message.Headers?.FirstOrDefault(h => h.Key == "messageType");
        
        if (messageTypeHeader == null)
        {
            _logger.LogWarning("Message without messageType header received from {Topic}:{Partition}:{Offset}", 
                consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);
            return Task.CompletedTask;
        }

        var messageType = System.Text.Encoding.UTF8.GetString(messageTypeHeader.GetValueBytes());
        

        try
        {
            object? envelope = messageType.ToUpperInvariant() switch
            {
                "EQUITY" => JsonSerializer.Deserialize<TradeMessageEnvelope<EquityTradeMessage>>(message.Value, _jsonOptions),
                "FX" => JsonSerializer.Deserialize<TradeMessageEnvelope<FxTradeMessage>>(message.Value, _jsonOptions),
                "OPTION" => JsonSerializer.Deserialize<TradeMessageEnvelope<OptionTradeMessage>>(message.Value, _jsonOptions),
                _ => throw new NotSupportedException($"Unknown message type: {messageType}")
            };
            
            var messageId = envelope switch
            {
                TradeMessageEnvelope<EquityTradeMessage> eq => eq.MessageId,
                TradeMessageEnvelope<FxTradeMessage> fx => fx.MessageId,
                TradeMessageEnvelope<OptionTradeMessage> opt => opt.MessageId,
                _ => "Unknown"
            };
            
            _logger.LogInformation("Processed {MessageType} message {MessageId} from {Topic}:{Partition}:{Offset}", 
                messageType, messageId, consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize message from {Topic}:{Partition}:{Offset}", 
                consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Unsupported message type {MessageType} from {Topic}:{Partition}:{Offset}", 
                messageType, consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);
        }
        
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}