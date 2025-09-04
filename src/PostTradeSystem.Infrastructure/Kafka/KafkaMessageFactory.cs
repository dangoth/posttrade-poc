using PostTradeSystem.Core.Messages;
using System.Text.Json;

namespace PostTradeSystem.Infrastructure.Kafka;
public static class KafkaMessageFactory
{
    /// <summary>
    /// Creates a standardized trade message envelope
    /// </summary>
    public static TradeMessageEnvelope<T> CreateTradeMessageEnvelope<T>(T message, string? correlationId = null) 
        where T : TradeMessage
    {
        return new TradeMessageEnvelope<T>
        {
            MessageId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            Version = "1.0",
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            Payload = message,
            Headers = CreateStandardHeaders(message)
        };
    }

    /// <summary>
    /// Creates standard headers for trade messages
    /// </summary>
    public static Dictionary<string, string> CreateStandardHeaders(TradeMessage message)
    {
        return new Dictionary<string, string>
        {
            { "messageType", message.MessageType },
            { "sourceSystem", message.SourceSystem },
            { "schemaVersion", "1.0" }
        };
    }

    /// <summary>
    /// Creates Kafka message headers from envelope headers
    /// </summary>
    public static Dictionary<string, byte[]> CreateKafkaHeaders(TradeMessage message)
    {
        return new Dictionary<string, byte[]>
        {
            { "messageType", System.Text.Encoding.UTF8.GetBytes(message.MessageType) },
            { "version", System.Text.Encoding.UTF8.GetBytes("1.0") },
            { "timestamp", System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()) }
        };
    }

    /// <summary>
    /// Serializes message envelope to JSON with standard options
    /// </summary>
    public static string SerializeEnvelope<T>(TradeMessageEnvelope<T> envelope) where T : TradeMessage
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        
        return JsonSerializer.Serialize(envelope, jsonOptions);
    }
}