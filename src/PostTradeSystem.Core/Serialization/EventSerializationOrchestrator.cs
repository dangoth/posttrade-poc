using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Services;

namespace PostTradeSystem.Core.Serialization;

public class EventSerializationOrchestrator
{
    private readonly IEventSerializer _eventSerializer;
    private readonly IEventVersionManager _versionManager;
    private readonly ISchemaRegistry _schemaRegistry;
    private readonly ITradeRiskService _tradeRiskService;

    public EventSerializationOrchestrator(
        IEventSerializer eventSerializer,
        IEventVersionManager versionManager,
        ISchemaRegistry schemaRegistry,
        ITradeRiskService tradeRiskService)
    {
        _eventSerializer = eventSerializer;
        _versionManager = versionManager;
        _schemaRegistry = schemaRegistry;
        _tradeRiskService = tradeRiskService;
    }

    public async Task<SerializedEvent> SerializeAsync<T>(T domainEvent, int? targetSchemaVersion = null) where T : IDomainEvent
    {
        return await _eventSerializer.SerializeAsync(domainEvent, targetSchemaVersion);
    }

    public async Task<IDomainEvent> DeserializeAsync(SerializedEvent serializedEvent)
    {
        return await _eventSerializer.DeserializeAsync(serializedEvent);
    }

    public bool CanHandle(string eventType, int schemaVersion)
    {
        return _versionManager.CanHandle(eventType, schemaVersion);
    }

    public IEnumerable<int> GetSupportedSchemaVersions(string eventType)
    {
        return _versionManager.GetSupportedVersions(eventType);
    }

    public int GetLatestSchemaVersion(string eventType)
    {
        return _versionManager.GetLatestVersion(eventType);
    }

    public async Task<ValidationResult> ValidateEventAsync(IDomainEvent domainEvent, int? targetVersion = null)
    {
        try
        {
            var serializedEvent = await SerializeAsync(domainEvent, targetVersion);
            return await ValidateEventDataAsync(serializedEvent.EventType, serializedEvent.Data, serializedEvent.SchemaVersion);
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Validation error: {ex.Message}");
        }
    }

    public async Task<ValidationResult> ValidateEventDataAsync(string eventType, string jsonData, int schemaVersion)
    {
        try
        {
            var isValid = await _schemaRegistry.ValidateSchemaAsync(eventType, jsonData, schemaVersion);
            return new ValidationResult(isValid, isValid ? null : $"Event {eventType} v{schemaVersion} failed schema validation");
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Validation error: {ex.Message}");
        }
    }

    public ITradeRiskService TradeRiskService => _tradeRiskService;
}