using Confluent.Kafka;
using PostTradeSystem.Core.Messages;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace PostTradeSystem.Infrastructure.Kafka;

public class KafkaProducerService : IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaSchemaRegistry _schemaRegistry;
    private readonly JsonSerializerOptions _jsonOptions;

    public KafkaProducerService(IConfiguration configuration, KafkaSchemaRegistry schemaRegistry)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = configuration.GetConnectionString("Kafka") ?? "localhost:9092",
            EnableIdempotence = true,
            Acks = Acks.All,
            MessageTimeoutMs = 30000,
            RequestTimeoutMs = 30000,
            RetryBackoffMs = 1000,
            MessageSendMaxRetries = 3
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
        _schemaRegistry = schemaRegistry;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<DeliveryResult<string, string>> ProduceAsync<T>(string topic, T message) where T : TradeMessage
    {
        var envelope = new TradeMessageEnvelope<T>
        {
            MessageId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            Version = "1.0",
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = message,
            Headers = new Dictionary<string, string>
            {
                { "messageType", message.MessageType },
                { "sourceSystem", message.SourceSystem },
                { "schemaVersion", "1.0" }
            }
        };

        var jsonMessage = JsonSerializer.Serialize(envelope, _jsonOptions);
        
        var isValid = await _schemaRegistry.ValidateMessageAsync(message.MessageType, jsonMessage);
        if (!isValid)
        {
            throw new InvalidOperationException($"Message validation failed for type {message.MessageType}");
        }

        var kafkaMessage = new Message<string, string>
        {
            Key = message.GetPartitionKey(),
            Value = jsonMessage,
            Headers = new Headers
            {
                { "messageType", System.Text.Encoding.UTF8.GetBytes(message.MessageType) },
                { "version", System.Text.Encoding.UTF8.GetBytes("1.0") },
                { "timestamp", System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()) }
            }
        };

        return await _producer.ProduceAsync(topic, kafkaMessage);
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
    }
}