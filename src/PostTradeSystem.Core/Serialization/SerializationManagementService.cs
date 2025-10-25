using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Serialization.Contracts;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Core.Common;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PostTradeSystem.Core.Serialization;

public class SerializationManagementService : ISerializationManagementService
{
    private readonly EventSerializationRegistry _registry;
    private readonly ISchemaRegistry _schemaRegistry;
    private readonly IJsonSchemaValidator _validator;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<string, SerializationInfo> _serializationCache = new();
    private readonly ITradeRiskService _tradeRiskService;

    public SerializationManagementService(
        EventSerializationRegistry registry,
        ISchemaRegistry schemaRegistry,
        IJsonSchemaValidator validator,
        ITradeRiskService tradeRiskService)
    {
        _registry = registry;
        _schemaRegistry = schemaRegistry;
        _validator = validator;
        _tradeRiskService = tradeRiskService;
        _jsonOptions = CreateJsonOptions();
    }

    public async Task InitializeAsync()
    {
        await RegisterEventContractsAsync();
        await RegisterEventConvertersAsync();
        await RegisterSchemasAsync();
    }

    #region Event Serialization

    public async Task<Result<SerializedEvent>> SerializeAsync(IDomainEvent domainEvent, int? targetSchemaVersion = null)
    {
        try
        {
            var eventType = GetEventTypeName(domainEvent);
            var schemaVersion = targetSchemaVersion ?? _registry.GetLatestSchemaVersion(eventType);
            
            var contractType = _registry.GetContractType(eventType, schemaVersion);
            if (contractType == null)
            {
                return Result<SerializedEvent>.Failure($"No contract registered for event type '{eventType}' schema version {schemaVersion}");
            }
            
            var contract = _registry.ConvertFromDomainEvent(domainEvent, schemaVersion);
            var jsonData = JsonSerializer.Serialize(contract, contract.GetType(), _jsonOptions);
            var schemaId = await GetSchemaIdAsync(eventType, schemaVersion);
            
            var validationResult = ValidateEventData(eventType, jsonData, schemaVersion);
            if (!validationResult.IsValid)
            {
            }
            
            var serializedEvent = new SerializedEvent(
                EventType: eventType,
                SchemaVersion: schemaVersion,
                Data: jsonData,
                SchemaId: schemaId,
                SerializedAt: DateTime.UtcNow,
                Metadata: CreateSerializationMetadata(domainEvent, contract));
                
            return Result<SerializedEvent>.Success(serializedEvent);
        }
        catch (Exception ex)
        {
            return Result<SerializedEvent>.Failure($"Failed to serialize event: {ex.Message}");
        }
    }

    public Result<IDomainEvent> Deserialize(SerializedEvent serializedEvent)
    {
        try
        {
            var contractType = _registry.GetContractType(serializedEvent.EventType, serializedEvent.SchemaVersion);
            if (contractType == null)
            {
                return Result<IDomainEvent>.Failure(
                    $"No contract registered for event type '{serializedEvent.EventType}' schema version {serializedEvent.SchemaVersion}");
            }

            var contract = (IVersionedEventContract)JsonSerializer.Deserialize(serializedEvent.Data, contractType, _jsonOptions)!;
            if (contract == null)
            {
                return Result<IDomainEvent>.Failure($"Failed to deserialize event data for type '{serializedEvent.EventType}'");
            }

            var latestSchemaVersion = _registry.GetLatestSchemaVersion(serializedEvent.EventType);
            if (contract.SchemaVersion != latestSchemaVersion)
            {
                var conversionResult = ConvertToLatestSchemaVersion(contract, latestSchemaVersion);
                if (conversionResult.IsFailure)
                    return Result<IDomainEvent>.Failure(conversionResult.Error);
                contract = conversionResult.Value!;
            }

            var domainEvent = _registry.ConvertToDomainEvent(contract);
            return Result<IDomainEvent>.Success(domainEvent);
        }
        catch (Exception ex)
        {
            return Result<IDomainEvent>.Failure($"Failed to deserialize event: {ex.Message}");
        }
    }

    #endregion

    #region Schema Management

    public ValidationResult ValidateEventData(string eventType, string jsonData, int schemaVersion)
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

    public ValidationResult ValidateEvent(IDomainEvent domainEvent, int? targetVersion = null)
    {
        try
        {
            var eventTypeName = GetEventTypeName(domainEvent);
            var schemaVersion = targetVersion ?? _registry.GetLatestSchemaVersion(eventTypeName);
            
            var contract = _registry.ConvertFromDomainEvent(domainEvent, schemaVersion);
            var json = JsonSerializer.Serialize(contract, _jsonOptions);
            
            return ValidateEventData(eventTypeName, json, schemaVersion);
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Validation error: {ex.Message}");
        }
    }

    public ValidationResult ValidateEventContract(IVersionedEventContract contract)
    {
        try
        {
            var json = JsonSerializer.Serialize(contract, _jsonOptions);
            return ValidateEventData(contract.EventType, json, contract.SchemaVersion);
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Validation error: {ex.Message}");
        }
    }

    #endregion

    #region Information Methods

    public bool CanHandle(string eventType, int schemaVersion)
    {
        return _registry.GetContractType(eventType, schemaVersion) != null;
    }

    public IEnumerable<int> GetSupportedSchemaVersions(string eventType)
    {
        return _registry.GetSupportedSchemaVersions(eventType);
    }

    public IEnumerable<string> GetSupportedEventTypes()
    {
        return new[] { "TradeCreated", "TradeStatusChanged", "TradeUpdated", "TradeEnriched", "TradeValidationFailed" };
    }

    public int GetLatestSchemaVersion(string eventType)
    {
        try
        {
            return _registry.GetLatestSchemaVersion(eventType);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"Event type '{eventType}' not registered. Available event types: [{string.Join(", ", GetSupportedEventTypes())}]", ex);
        }
    }

    public IEnumerable<SerializationInfo> GetCachedSerializationInfo()
    {
        return _serializationCache.Values;
    }

    #endregion

    #region Private Methods

    private async Task RegisterEventContractsAsync()
    {
        // TradeCreated Event - Register V2 first so it becomes the latest version for serialization
        _registry.RegisterContract<TradeCreatedEventV2>(
            domainEvent => ConvertTradeCreatedEventToV2((TradeCreatedEvent)domainEvent),
            contract => ConvertV2ToTradeCreatedEvent(contract));

        // TradeCreated V1 - Only for deserialization of legacy events
        _registry.RegisterContract<TradeCreatedEventV1>(
            domainEvent => throw new InvalidOperationException("V1 should not be used for serialization - use V2"),
            contract => ConvertV1ToTradeCreatedEvent(contract));

        // TradeStatusChanged Event - Register V2 first so it becomes the latest version for serialization
        _registry.RegisterContract<TradeStatusChangedEventV2>(
            domainEvent => ConvertTradeStatusChangedEventToV2((TradeStatusChangedEvent)domainEvent),
            contract => ConvertV2ToTradeStatusChangedEvent(contract));

        // TradeStatusChanged V1 - Only for deserialization of legacy events
        _registry.RegisterContract<TradeStatusChangedEventV1>(
            domainEvent => throw new InvalidOperationException("V1 should not be used for serialization - use V2"),
            contract => ConvertV1ToTradeStatusChangedEvent(contract));

        // Single version events
        _registry.RegisterContract<TradeUpdatedEventV1>(
            domainEvent => ConvertTradeUpdatedEventToV1((TradeUpdatedEvent)domainEvent),
            contract => ConvertV1ToTradeUpdatedEvent(contract));

        _registry.RegisterContract<TradeEnrichedEventV1>(
            domainEvent => ConvertTradeEnrichedEventToV1((TradeEnrichedEvent)domainEvent),
            contract => ConvertV1ToTradeEnrichedEvent(contract));

        _registry.RegisterContract<TradeValidationFailedEventV1>(
            domainEvent => ConvertTradeValidationFailedEventToV1((TradeValidationFailedEvent)domainEvent),
            contract => ConvertV1ToTradeValidationFailedEvent(contract));

        await Task.CompletedTask;
    }

    private async Task RegisterEventConvertersAsync()
    {
        var mockExternalDataService = new DeterministicMockExternalDataService();
        _registry.RegisterConverter(new TradeCreatedEventV1ToV2Converter(mockExternalDataService));
        _registry.RegisterConverter(new TradeCreatedEventV2ToV1Converter());
        _registry.RegisterConverter(new TradeStatusChangedEventV1ToV2Converter(mockExternalDataService));
        _registry.RegisterConverter(new TradeStatusChangedEventV2ToV1Converter());

        await Task.CompletedTask;
    }

    private async Task RegisterSchemasAsync()
    {
        var eventSchemas = new Dictionary<string, string>
        {
            { "TradeCreated-v1", EventSchemas.TradeCreatedEventV1Schema },
            { "TradeCreated-v2", EventSchemas.TradeCreatedEventV2Schema },
            { "TradeStatusChanged-v1", EventSchemas.TradeStatusChangedEventV1Schema },
            { "TradeStatusChanged-v2", EventSchemas.TradeStatusChangedEventV2Schema }
        };

        foreach (var (schemaKey, schema) in eventSchemas)
        {
            _validator.RegisterSchema(schemaKey, schema);
            
            // Also register with just the event type for validation
            var parts = schemaKey.Split('-');
            if (parts.Length == 2)
            {
                var eventType = parts[0];
                _validator.RegisterSchema(eventType, schema);
            }
            
            var subject = $"event-{schemaKey.ToLower()}";
            // Extract version from schemaKey (e.g., "TradeCreated-v2" -> 2)
            var version = ExtractVersionFromSchemaKey(schemaKey);
            var schemaId = await _schemaRegistry.RegisterSchemaAsync(subject, schema, version);
            
            // Cache schema info
            _serializationCache[schemaKey] = new SerializationInfo(schemaKey, schema, schemaId);
        }
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

    private static Dictionary<string, string> CreateSerializationMetadata(IDomainEvent domainEvent, IVersionedEventContract contract)
    {
        return new Dictionary<string, string>
        {
            ["serializer"] = "SerializationManagementService",
            ["domainEventType"] = domainEvent.GetType().FullName!,
            ["contractType"] = contract.GetType().FullName!,
            ["serializedAt"] = DateTime.UtcNow.ToString("O")
        };
    }

    private static int ExtractVersionFromSchemaKey(string schemaKey)
    {
        // Extract version from schemaKey like "TradeCreated-v2" -> 2
        var parts = schemaKey.Split('-');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("v") && int.TryParse(parts[i][1..], out var version))
            {
                return version;
            }
        }
        
        // Default to version 1 if no version found
        return 1;
    }

    private Result<IVersionedEventContract> ConvertToLatestSchemaVersion(IVersionedEventContract contract, int targetSchemaVersion)
    {
        if (contract.SchemaVersion == targetSchemaVersion)
        {
            return Result<IVersionedEventContract>.Success(contract);
        }

        var currentSchemaVersion = contract.SchemaVersion;
        var current = contract;

        while (currentSchemaVersion < targetSchemaVersion)
        {
            var nextSchemaVersion = currentSchemaVersion + 1;
            var conversionResult = ConvertToNextSchemaVersion(current, nextSchemaVersion);
            if (conversionResult.IsFailure)
                return conversionResult;
            current = conversionResult.Value!;
            currentSchemaVersion = nextSchemaVersion;
        }

        return Result<IVersionedEventContract>.Success(current);
    }

    private Result<IVersionedEventContract> ConvertToNextSchemaVersion(IVersionedEventContract contract, int targetSchemaVersion)
    {
        try
        {
            var eventType = contract.EventType;
            
            if (eventType == "TradeCreated" && contract.SchemaVersion == 1 && targetSchemaVersion == 2)
            {
                var converted = _registry.ConvertVersion<TradeCreatedEventV1, TradeCreatedEventV2>((TradeCreatedEventV1)contract);
                return Result<IVersionedEventContract>.Success(converted);
            }
            
            if (eventType == "TradeStatusChanged" && contract.SchemaVersion == 1 && targetSchemaVersion == 2)
            {
                var converted = _registry.ConvertVersion<TradeStatusChangedEventV1, TradeStatusChangedEventV2>((TradeStatusChangedEventV1)contract);
                return Result<IVersionedEventContract>.Success(converted);
            }

            return Result<IVersionedEventContract>.Failure($"No converter available for {eventType} from schema version {contract.SchemaVersion} to {targetSchemaVersion}");
        }
        catch (Exception ex)
        {
            return Result<IVersionedEventContract>.Failure($"Failed to convert schema version: {ex.Message}");
        }
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
            Converters = { 
                new JsonStringEnumConverter(),
                new DictionaryObjectJsonConverter()
            }
        };
    }

    #region Event Conversion Methods

    private TradeCreatedEventV2 ConvertTradeCreatedEventToV2(TradeCreatedEvent domainEvent)
    {
        return new TradeCreatedEventV2
        {
            EventId = domainEvent.EventId,
            AggregateId = domainEvent.AggregateId,
            AggregateType = domainEvent.AggregateType,
            OccurredAt = domainEvent.OccurredAt,
            AggregateVersion = domainEvent.AggregateVersion,
            CorrelationId = domainEvent.CorrelationId,
            CausedBy = domainEvent.CausedBy,
            TraderId = domainEvent.TraderId,
            InstrumentId = domainEvent.InstrumentId,
            Quantity = domainEvent.Quantity,
            Price = domainEvent.Price,
            Direction = domainEvent.Direction,
            TradeDateTime = domainEvent.TradeDateTime,
            Currency = domainEvent.Currency,
            CounterpartyId = domainEvent.CounterpartyId,
            TradeType = domainEvent.TradeType,
            AdditionalData = new Dictionary<string, object>(domainEvent.AdditionalData),
            RiskProfile = _tradeRiskService.ExtractRiskProfile(domainEvent.AdditionalData),
            NotionalValue = _tradeRiskService.CalculateNotionalValue(domainEvent.Quantity, domainEvent.Price),
            RegulatoryClassification = _tradeRiskService.DetermineRegulatoryClassification(domainEvent.TradeType)
        };
    }

    private static TradeCreatedEvent ConvertV1ToTradeCreatedEvent(TradeCreatedEventV1 contract)
    {
        var domainEvent = new TradeCreatedEvent(
            contract.AggregateId,
            contract.TraderId,
            contract.InstrumentId,
            contract.Quantity,
            contract.Price,
            contract.Direction,
            contract.TradeDateTime,
            contract.Currency,
            contract.CounterpartyId,
            contract.TradeType,
            contract.AggregateVersion,
            contract.CorrelationId,
            contract.CausedBy,
            contract.AdditionalData);
            
        // Set the EventId and OccurredAt - private setters :(
        typeof(DomainEventBase).GetProperty("EventId")?.SetValue(domainEvent, contract.EventId);
        typeof(DomainEventBase).GetProperty("OccurredAt")?.SetValue(domainEvent, contract.OccurredAt);
        
        return domainEvent;
    }

    private static TradeCreatedEvent ConvertV2ToTradeCreatedEvent(TradeCreatedEventV2 contract)
    {
        var additionalData = new Dictionary<string, object>(contract.AdditionalData)
        {
            ["riskProfile"] = contract.RiskProfile,
            ["notionalValue"] = contract.NotionalValue,
            ["regulatoryClassification"] = contract.RegulatoryClassification
        };

        var domainEvent = new TradeCreatedEvent(
            contract.AggregateId,
            contract.TraderId,
            contract.InstrumentId,
            contract.Quantity,
            contract.Price,
            contract.Direction,
            contract.TradeDateTime,
            contract.Currency,
            contract.CounterpartyId,
            contract.TradeType,
            contract.AggregateVersion,
            contract.CorrelationId,
            contract.CausedBy,
            additionalData);
            
        // Set the EventId and OccurredAt - private setters :(
        typeof(DomainEventBase).GetProperty("EventId")?.SetValue(domainEvent, contract.EventId);
        typeof(DomainEventBase).GetProperty("OccurredAt")?.SetValue(domainEvent, contract.OccurredAt);
        
        return domainEvent;
    }

    private static TradeStatusChangedEventV2 ConvertTradeStatusChangedEventToV2(TradeStatusChangedEvent domainEvent)
    {
        return new TradeStatusChangedEventV2
        {
            EventId = domainEvent.EventId,
            AggregateId = domainEvent.AggregateId,
            AggregateType = domainEvent.AggregateType,
            OccurredAt = domainEvent.OccurredAt,
            AggregateVersion = domainEvent.AggregateVersion,
            CorrelationId = domainEvent.CorrelationId,
            CausedBy = domainEvent.CausedBy,
            PreviousStatus = domainEvent.PreviousStatus,
            NewStatus = domainEvent.NewStatus,
            Reason = domainEvent.Reason,
            ApprovedBy = domainEvent.CausedBy,
            ApprovalTimestamp = domainEvent.OccurredAt,
            AuditTrail = $"Status changed from {domainEvent.PreviousStatus} to {domainEvent.NewStatus}. Reason: {domainEvent.Reason}"
        };
    }

    private static TradeStatusChangedEvent ConvertV1ToTradeStatusChangedEvent(TradeStatusChangedEventV1 contract)
    {
        return new TradeStatusChangedEvent(
            contract.AggregateId,
            contract.PreviousStatus,
            contract.NewStatus,
            contract.Reason,
            contract.AggregateVersion,
            contract.CorrelationId,
            contract.CausedBy);
    }

    private static TradeStatusChangedEvent ConvertV2ToTradeStatusChangedEvent(TradeStatusChangedEventV2 contract)
    {
        return new TradeStatusChangedEvent(
            contract.AggregateId,
            contract.PreviousStatus,
            contract.NewStatus,
            contract.Reason,
            contract.AggregateVersion,
            contract.CorrelationId,
            contract.CausedBy);
    }

    private static TradeUpdatedEventV1 ConvertTradeUpdatedEventToV1(TradeUpdatedEvent domainEvent)
    {
        return new TradeUpdatedEventV1
        {
            EventId = domainEvent.EventId,
            AggregateId = domainEvent.AggregateId,
            AggregateType = domainEvent.AggregateType,
            OccurredAt = domainEvent.OccurredAt,
            AggregateVersion = domainEvent.AggregateVersion,
            CorrelationId = domainEvent.CorrelationId,
            CausedBy = domainEvent.CausedBy,
            UpdatedFields = new Dictionary<string, object>(domainEvent.UpdatedFields)
        };
    }

    private static TradeUpdatedEvent ConvertV1ToTradeUpdatedEvent(TradeUpdatedEventV1 contract)
    {
        return new TradeUpdatedEvent(
            contract.AggregateId,
            contract.UpdatedFields,
            contract.AggregateVersion,
            contract.CorrelationId,
            contract.CausedBy);
    }

    private static TradeEnrichedEventV1 ConvertTradeEnrichedEventToV1(TradeEnrichedEvent domainEvent)
    {
        return new TradeEnrichedEventV1
        {
            EventId = domainEvent.EventId,
            AggregateId = domainEvent.AggregateId,
            AggregateType = domainEvent.AggregateType,
            OccurredAt = domainEvent.OccurredAt,
            AggregateVersion = domainEvent.AggregateVersion,
            CorrelationId = domainEvent.CorrelationId,
            CausedBy = domainEvent.CausedBy,
            EnrichmentType = domainEvent.EnrichmentType,
            EnrichmentData = new Dictionary<string, object>(domainEvent.EnrichmentData)
        };
    }

    private static TradeEnrichedEvent ConvertV1ToTradeEnrichedEvent(TradeEnrichedEventV1 contract)
    {
        return new TradeEnrichedEvent(
            contract.AggregateId,
            contract.EnrichmentType,
            contract.EnrichmentData,
            contract.AggregateVersion,
            contract.CorrelationId,
            contract.CausedBy);
    }

    private static TradeValidationFailedEventV1 ConvertTradeValidationFailedEventToV1(TradeValidationFailedEvent domainEvent)
    {
        return new TradeValidationFailedEventV1
        {
            EventId = domainEvent.EventId,
            AggregateId = domainEvent.AggregateId,
            AggregateType = domainEvent.AggregateType,
            OccurredAt = domainEvent.OccurredAt,
            AggregateVersion = domainEvent.AggregateVersion,
            CorrelationId = domainEvent.CorrelationId,
            CausedBy = domainEvent.CausedBy,
            ValidationErrors = new List<string>(domainEvent.ValidationErrors)
        };
    }

    private static TradeValidationFailedEvent ConvertV1ToTradeValidationFailedEvent(TradeValidationFailedEventV1 contract)
    {
        return new TradeValidationFailedEvent(
            contract.AggregateId,
            contract.ValidationErrors,
            contract.AggregateVersion,
            contract.CorrelationId,
            contract.CausedBy);
    }


    #endregion

    #endregion
}

public record SerializationInfo(string EventType, string Schema, int SchemaId);

// Contract classes for single-version events
public class TradeUpdatedEventV1 : VersionedEventContractBase
{
    public override int SchemaVersion => 1;
    public override string EventType => "TradeUpdated";

    public string EventId { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public long AggregateVersion { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string CausedBy { get; set; } = string.Empty;
    public Dictionary<string, object> UpdatedFields { get; set; } = new();
}

public class TradeEnrichedEventV1 : VersionedEventContractBase
{
    public override int SchemaVersion => 1;
    public override string EventType => "TradeEnriched";

    public string EventId { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public long AggregateVersion { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string CausedBy { get; set; } = string.Empty;
    public string EnrichmentType { get; set; } = string.Empty;
    public Dictionary<string, object> EnrichmentData { get; set; } = new();
}

public class TradeValidationFailedEventV1 : VersionedEventContractBase
{
    public override int SchemaVersion => 1;
    public override string EventType => "TradeValidationFailed";

    public string EventId { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public long AggregateVersion { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string CausedBy { get; set; } = string.Empty;
    public List<string> ValidationErrors { get; set; } = new();
}

