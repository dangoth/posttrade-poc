using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Serialization.Contracts;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PostTradeSystem.Core.Serialization;

public interface IEventSerializer
{
    Task<SerializedEvent> SerializeAsync<T>(T domainEvent, int? targetSchemaVersion = null) where T : IDomainEvent;
    Task<IDomainEvent> DeserializeAsync(SerializedEvent serializedEvent);
}

public class EventSerializer : IEventSerializer
{
    private readonly IEventVersionManager _versionManager;
    private readonly ISchemaRegistry _schemaRegistry;
    private readonly JsonSchemaValidator _validator;
    private readonly JsonSerializerOptions _jsonOptions;

    public EventSerializer(
        IEventVersionManager versionManager,
        ISchemaRegistry schemaRegistry,
        JsonSchemaValidator validator)
    {
        _versionManager = versionManager;
        _schemaRegistry = schemaRegistry;
        _validator = validator;
        _jsonOptions = CreateJsonOptions();
    }

    public async Task<SerializedEvent> SerializeAsync<T>(T domainEvent, int? targetSchemaVersion = null) where T : IDomainEvent
    {
        var eventType = GetEventTypeName(domainEvent);
        var schemaVersion = targetSchemaVersion ?? _versionManager.GetLatestVersion(eventType);
        
        var contractType = _versionManager.GetContractType(eventType, schemaVersion);
        if (contractType == null)
        {
            throw new InvalidOperationException(
                $"No contract registered for event type '{eventType}' schema version {schemaVersion}. " +
                $"Available versions: [{string.Join(", ", _versionManager.GetSupportedVersions(eventType))}]");
        }
        
        var contract = _versionManager.ConvertFromDomainEvent(domainEvent, schemaVersion);
        var jsonData = JsonSerializer.Serialize(contract, contract.GetType(), _jsonOptions);
        var schemaId = await GetSchemaIdAsync(eventType, schemaVersion);
        
        try
        {
            var validationResult = ValidateEventData(eventType, jsonData, schemaVersion);
            if (!validationResult.IsValid)
            {
                System.Diagnostics.Debug.WriteLine($"Schema validation warning: {validationResult.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Schema validation error: {ex.Message}");
        }
        
        return new SerializedEvent(
            EventType: eventType,
            SchemaVersion: schemaVersion,
            Data: jsonData,
            SchemaId: schemaId,
            SerializedAt: DateTime.UtcNow,
            Metadata: CreateSerializationMetadata(domainEvent, contract));
    }

    public Task<IDomainEvent> DeserializeAsync(SerializedEvent serializedEvent)
    {
        var contractType = _versionManager.GetContractType(serializedEvent.EventType, serializedEvent.SchemaVersion);
        if (contractType == null)
        {
            throw new InvalidOperationException(
                $"No contract registered for event type '{serializedEvent.EventType}' schema version {serializedEvent.SchemaVersion}");
        }

        var contract = (IVersionedEventContract)JsonSerializer.Deserialize(serializedEvent.Data, contractType, _jsonOptions)!;

        var latestSchemaVersion = _versionManager.GetLatestVersion(serializedEvent.EventType);
        if (contract.SchemaVersion != latestSchemaVersion)
        {
            contract = ConvertToLatestSchemaVersion(contract, latestSchemaVersion);
        }

        var result = _versionManager.ConvertToDomainEvent(contract);
        return Task.FromResult(result);
    }

    private static string GetEventTypeName(IDomainEvent domainEvent)
    {
        var typeName = domainEvent.GetType().Name;
        return typeName.EndsWith("Event") ? typeName[..^5] : typeName;
    }

    private async Task<string> GetSchemaIdAsync(string eventType, int schemaVersion)
    {
        try
        {
            var subject = $"event-{eventType.ToLower()}-v{schemaVersion}";
            var schema = await _schemaRegistry.GetLatestSchemaAsync(subject);
            return schema.Id.ToString();
        }
        catch
        {
            return "unknown";
        }
    }

    private ValidationResult ValidateEventData(string eventType, string jsonData, int schemaVersion)
    {
        try
        {
            var isValid = _validator.ValidateMessage(eventType, jsonData, schemaVersion);
            return new ValidationResult(isValid, isValid ? null : $"Event {eventType} v{schemaVersion} failed schema validation");
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Validation error: {ex.Message}");
        }
    }

    private static Dictionary<string, string> CreateSerializationMetadata(IDomainEvent domainEvent, IVersionedEventContract contract)
    {
        return new Dictionary<string, string>
        {
            ["serializer"] = "EventSerializer",
            ["domainEventType"] = domainEvent.GetType().FullName!,
            ["contractType"] = contract.GetType().FullName!,
            ["serializedAt"] = DateTime.UtcNow.ToString("O")
        };
    }

    private IVersionedEventContract ConvertToLatestSchemaVersion(IVersionedEventContract contract, int targetSchemaVersion)
    {
        if (contract.SchemaVersion == targetSchemaVersion)
        {
            return contract;
        }

        var currentSchemaVersion = contract.SchemaVersion;
        var current = contract;

        while (currentSchemaVersion < targetSchemaVersion)
        {
            var nextSchemaVersion = currentSchemaVersion + 1;
            current = ConvertToNextSchemaVersion(current, nextSchemaVersion);
            currentSchemaVersion = nextSchemaVersion;
        }

        return current;
    }

    private IVersionedEventContract ConvertToNextSchemaVersion(IVersionedEventContract contract, int targetSchemaVersion)
    {
        var eventType = contract.EventType;
        
        if (eventType == "TradeCreated" && contract.SchemaVersion == 1 && targetSchemaVersion == 2)
        {
            return _versionManager.ConvertToVersion<TradeCreatedEventV2>(contract, targetSchemaVersion);
        }
        
        if (eventType == "TradeStatusChanged" && contract.SchemaVersion == 1 && targetSchemaVersion == 2)
        {
            return _versionManager.ConvertToVersion<TradeStatusChangedEventV2>(contract, targetSchemaVersion);
        }

        throw new InvalidOperationException($"No converter available for {eventType} from schema version {contract.SchemaVersion} to {targetSchemaVersion}");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            IgnoreReadOnlyProperties = false,
            IgnoreReadOnlyFields = false,
            IncludeFields = false,
            Converters = { new JsonStringEnumConverter() }
        };
    }
}