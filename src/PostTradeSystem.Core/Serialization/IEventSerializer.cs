using PostTradeSystem.Core.Events;

namespace PostTradeSystem.Core.Serialization;

public record SerializedEvent(
    string EventType,
    int SchemaVersion,
    string Data,
    string SchemaId,
    DateTime SerializedAt,
    Dictionary<string, string> Metadata);