using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Schemas;
using System.Text;

namespace PostTradeSystem.Infrastructure.Serialization;

public class KafkaEventSerializer
{
    private readonly IEventSerializer _eventSerializer;
    private readonly ISchemaRegistry _schemaRegistry;

    public KafkaEventSerializer(IEventSerializer eventSerializer, ISchemaRegistry schemaRegistry)
    {
        _eventSerializer = eventSerializer;
        _schemaRegistry = schemaRegistry;
    }

    public async Task<KafkaMessage> SerializeForKafkaAsync(IDomainEvent domainEvent)
    {
        var serializedEvent = await _eventSerializer.Serialize(domainEvent);
        
        var headers = new Dictionary<string, byte[]>
        {
            ["event-type"] = Encoding.UTF8.GetBytes(serializedEvent.EventType),
            ["event-version"] = Encoding.UTF8.GetBytes(serializedEvent.Version.ToString()),
            ["schema-id"] = Encoding.UTF8.GetBytes(serializedEvent.SchemaId),
            ["serialized-at"] = Encoding.UTF8.GetBytes(serializedEvent.SerializedAt.ToString("O")),
            ["correlation-id"] = Encoding.UTF8.GetBytes(domainEvent.CorrelationId),
            ["caused-by"] = Encoding.UTF8.GetBytes(domainEvent.CausedBy),
            ["aggregate-version"] = Encoding.UTF8.GetBytes(domainEvent.AggregateVersion.ToString())
        };

        foreach (var metadata in serializedEvent.Metadata)
        {
            headers[$"meta-{metadata.Key}"] = Encoding.UTF8.GetBytes(metadata.Value);
        }

        return new KafkaMessage(
            Key: domainEvent.AggregateId,
            Value: serializedEvent.Data,
            Headers: headers,
            Partition: CalculatePartition(domainEvent.AggregateId),
            Topic: GetTopicName(serializedEvent.EventType));
    }

    public IDomainEvent DeserializeFromKafka(KafkaMessage kafkaMessage)
    {
        var eventType = GetHeaderValue(kafkaMessage.Headers, "event-type");
        var version = int.Parse(GetHeaderValue(kafkaMessage.Headers, "event-version"));
        var schemaId = GetHeaderValue(kafkaMessage.Headers, "schema-id");
        var serializedAt = DateTime.Parse(GetHeaderValue(kafkaMessage.Headers, "serialized-at"));

        var metadata = new Dictionary<string, string>();
        foreach (var header in kafkaMessage.Headers.Where(h => h.Key.StartsWith("meta-")))
        {
            var key = header.Key[5..]; // Remove "meta-" prefix
            metadata[key] = Encoding.UTF8.GetString(header.Value);
        }

        var serializedEvent = new SerializedEvent(
            eventType, version, kafkaMessage.Value, schemaId, serializedAt, metadata);

        return _eventSerializer.Deserialize(serializedEvent);
    }

    public bool CanDeserialize(KafkaMessage kafkaMessage)
    {
        try
        {
            if (!kafkaMessage.Headers.ContainsKey("event-type") || 
                !kafkaMessage.Headers.ContainsKey("event-version"))
            {
                return false;
            }

            var eventType = GetHeaderValue(kafkaMessage.Headers, "event-type");
            var version = int.Parse(GetHeaderValue(kafkaMessage.Headers, "event-version"));

            return _eventSerializer.CanHandle(eventType, version);
        }
        catch
        {
            return false;
        }
    }

    private static string GetHeaderValue(Dictionary<string, byte[]> headers, string key)
    {
        return headers.TryGetValue(key, out var value) 
            ? Encoding.UTF8.GetString(value) 
            : throw new ArgumentException($"Header '{key}' not found");
    }

    private static int CalculatePartition(string aggregateId)
    {
        var hash = aggregateId.GetHashCode();
        return Math.Abs(hash) % 3;
    }

    private static string GetTopicName(string eventType)
    {
        return eventType.ToLower() switch
        {
            "tradecreated" => "trades.equities",
            "tradestatuschanged" => "trades.equities", 
            "tradeupdated" => "trades.equities",
            "tradeenriched" => "trades.equities",
            "tradevalidationfailed" => "trades.equities",
            _ => "trades.general"
        };
    }
}

public record KafkaMessage(
    string Key,
    string Value,
    Dictionary<string, byte[]> Headers,
    int Partition,
    string Topic);