using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Infrastructure.Kafka;
using System.Text;

namespace PostTradeSystem.Infrastructure.Serialization;

public class KafkaEventSerializer
{
    private readonly IEventSerializer _eventSerializer;
    private readonly ISerializationManagementService _serializationService;

    public KafkaEventSerializer(IEventSerializer eventSerializer, ISerializationManagementService serializationService)
    {
        _eventSerializer = eventSerializer;
        _serializationService = serializationService;
    }

    public async Task<KafkaMessage> SerializeForKafkaAsync(IDomainEvent domainEvent)
    {
        var serializedEvent = await _eventSerializer.SerializeAsync(domainEvent);
        
        var headers = KafkaHeaderUtility.CreateEventHeaders(
            serializedEvent.EventType,
            serializedEvent.SchemaVersion,
            serializedEvent.SchemaId,
            serializedEvent.SerializedAt,
            domainEvent.CorrelationId,
            domainEvent.CausedBy,
            domainEvent.AggregateVersion,
            serializedEvent.Metadata);

        return new KafkaMessage(
            Key: domainEvent.AggregateId,
            Value: serializedEvent.Data,
            Headers: headers,
            Partition: CalculatePartition(domainEvent.AggregateId),
            Topic: GetTopicName(serializedEvent.EventType));
    }

    public async Task<IDomainEvent> DeserializeFromKafka(KafkaMessage kafkaMessage)
    {
        var eventType = KafkaHeaderUtility.GetHeaderValue(kafkaMessage.Headers, "event-type");
        var version = int.Parse(KafkaHeaderUtility.GetHeaderValue(kafkaMessage.Headers, "event-version"));
        var schemaId = KafkaHeaderUtility.GetHeaderValue(kafkaMessage.Headers, "schema-id");
        var serializedAt = DateTime.Parse(KafkaHeaderUtility.GetHeaderValue(kafkaMessage.Headers, "serialized-at"));

        var metadata = KafkaHeaderUtility.ExtractMetadataHeaders(kafkaMessage.Headers);

        var serializedEvent = new SerializedEvent(
            eventType, version, kafkaMessage.Value, schemaId, serializedAt, metadata);

        return await _eventSerializer.DeserializeAsync(serializedEvent);
    }

    public bool CanDeserialize(KafkaMessage kafkaMessage)
    {
        try
        {
            if (!KafkaHeaderUtility.HasRequiredHeaders(kafkaMessage.Headers, "event-type", "event-version"))
            {
                return false;
            }

            var eventType = KafkaHeaderUtility.GetHeaderValue(kafkaMessage.Headers, "event-type");
            var version = int.Parse(KafkaHeaderUtility.GetHeaderValue(kafkaMessage.Headers, "event-version"));

            return _serializationService.CanHandle(eventType, version);
        }
        catch
        {
            return false;
        }
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