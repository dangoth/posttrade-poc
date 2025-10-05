using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Serialization.Contracts;

namespace PostTradeSystem.Core.Serialization;

public interface ISerializationManagementService
{
    Task InitializeAsync();
    
    Task<SerializedEvent> SerializeAsync(IDomainEvent domainEvent, int? targetSchemaVersion = null);
    IDomainEvent Deserialize(SerializedEvent serializedEvent);
    
    ValidationResult ValidateEventData(string eventType, string jsonData, int schemaVersion);
    ValidationResult ValidateEvent(IDomainEvent domainEvent, int? targetVersion = null);
    ValidationResult ValidateEventContract(IVersionedEventContract contract);
    
    bool CanHandle(string eventType, int schemaVersion);
    IEnumerable<int> GetSupportedSchemaVersions(string eventType);
    IEnumerable<string> GetSupportedEventTypes();
    int GetLatestSchemaVersion(string eventType);
    IEnumerable<SerializationInfo> GetCachedSerializationInfo();
}