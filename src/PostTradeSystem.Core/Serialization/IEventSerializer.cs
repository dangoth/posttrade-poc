using PostTradeSystem.Core.Events;

namespace PostTradeSystem.Core.Serialization;

public interface IEventSerializer
{
    Task<SerializedEvent> Serialize(IDomainEvent domainEvent);
    IDomainEvent Deserialize(SerializedEvent serializedEvent);
    bool CanHandle(string eventType, int schemaVersion);
    IEnumerable<int> GetSupportedSchemaVersions(string eventType);
}

public record SerializedEvent(
    string EventType,
    int SchemaVersion,
    string Data,
    string SchemaId,
    DateTime SerializedAt,
    Dictionary<string, string> Metadata);