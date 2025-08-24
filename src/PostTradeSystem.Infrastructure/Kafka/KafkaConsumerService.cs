using Confluent.Kafka;
using PostTradeSystem.Core.Messages;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace PostTradeSystem.Infrastructure.Kafka;

public class KafkaConsumerService : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly KafkaSchemaRegistry _schemaRegistry;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string[] _topics;

    public KafkaConsumerService(
        IConfiguration configuration, 
        KafkaSchemaRegistry schemaRegistry,
        ILogger<KafkaConsumerService> logger)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = configuration.GetConnectionString("Kafka") ?? "localhost:9092",
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

        _schemaRegistry = schemaRegistry;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        _topics = new[] { "trades.equities", "trades.options", "trades.fx" };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(_topics);
        _logger.LogInformation("Kafka consumer started, subscribed to topics: {Topics}", string.Join(", ", _topics));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(stoppingToken);
                    
                    if (consumeResult?.Message != null)
                    {
                        await ProcessMessageAsync(consumeResult);
                        _consumer.Commit(consumeResult);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message: {Error}", ex.Error.Reason);
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
        finally
        {
            _consumer.Close();
            _logger.LogInformation("Kafka consumer stopped");
        }
    }

    private async Task ProcessMessageAsync(ConsumeResult<string, string> consumeResult)
    {
        var message = consumeResult.Message;
        var messageTypeHeader = message.Headers?.FirstOrDefault(h => h.Key == "messageType");
        
        if (messageTypeHeader == null)
        {
            _logger.LogWarning("Message without messageType header received from {Topic}:{Partition}:{Offset}", 
                consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);
            return;
        }

        var messageType = System.Text.Encoding.UTF8.GetString(messageTypeHeader.GetValueBytes());
        
        var isValid = await _schemaRegistry.ValidateMessageAsync(messageType, message.Value);
        if (!isValid)
        {
            _logger.LogError("Schema validation failed for message type {MessageType} from {Topic}:{Partition}:{Offset}", 
                messageType, consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);
            return;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<TradeMessageEnvelope<TradeMessage>>(message.Value, _jsonOptions);
            
            _logger.LogInformation("Processed {MessageType} message {MessageId} from {Topic}:{Partition}:{Offset}", 
                messageType, envelope?.MessageId, consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize message from {Topic}:{Partition}:{Offset}", 
                consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);
        }
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}