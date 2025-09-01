using PostTradeSystem.Core.Events;

namespace PostTradeSystem.Core.Serialization;

public interface IEventSerializer
{
    Task<SerializedEvent> Serialize(IDomainEvent domainEvent);
    IDomainEvent Deserialize(SerializedEvent serializedEvent);
    bool CanHandle(string eventType, int version);
    IEnumerable<int> GetSupportedVersions(string eventType);
}

public record SerializedEvent(
    string EventType,
    int Version,
    string Data,
    string SchemaId,
    DateTime SerializedAt,
    Dictionary<string, string> Metadata);