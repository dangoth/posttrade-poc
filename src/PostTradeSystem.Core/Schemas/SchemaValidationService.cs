using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.OutputFormats;
using System.Text.Json;

namespace PostTradeSystem.Core.Schemas;

public class SchemaValidationService
{
    private readonly JsonSchemaValidator _validator;
    private readonly EventSerializationRegistry _eventRegistry;

    public SchemaValidationService(JsonSchemaValidator validator, EventSerializationRegistry eventRegistry)
    {
        _validator = validator;
        _eventRegistry = eventRegistry;
        RegisterAllSchemas();
    }

    private void RegisterAllSchemas()
    {
        RegisterEventSchemas();
        RegisterMessageSchemas();
        RegisterOutputSchemas();
    }

    private void RegisterEventSchemas()
    {
        _validator.RegisterSchema("TradeCreated-v1", EventSchemas.TradeCreatedEventV1Schema);
        _validator.RegisterSchema("TradeCreated-v2", EventSchemas.TradeCreatedEventV2Schema);
        _validator.RegisterSchema("TradeStatusChanged-v1", EventSchemas.TradeStatusChangedEventV1Schema);
        _validator.RegisterSchema("TradeStatusChanged-v2", EventSchemas.TradeStatusChangedEventV2Schema);
    }

    private void RegisterMessageSchemas()
    {
        _validator.RegisterSchema("TradeMessage", MessageSchemas.TradeMessageSchema);
        _validator.RegisterSchema("EquityTradeMessage", MessageSchemas.EquityTradeMessageSchema);
        _validator.RegisterSchema("OptionTradeMessage", MessageSchemas.OptionTradeMessageSchema);
        _validator.RegisterSchema("FxTradeMessage", MessageSchemas.FxTradeMessageSchema);
        _validator.RegisterSchema("TradeMessageEnvelope", MessageSchemas.TradeMessageEnvelopeSchema);
    }

    private void RegisterOutputSchemas()
    {
        _validator.RegisterSchema("ComplianceOutput", EventSchemas.ComplianceOutputSchema);
        _validator.RegisterSchema("RiskManagementOutput", EventSchemas.RiskManagementOutputSchema);
    }

    public ValidationResult ValidateEvent(IDomainEvent domainEvent, int? targetVersion = null)
    {
        try
        {
            var eventTypeName = domainEvent.GetType().Name.Replace("Event", "");
            var version = targetVersion ?? _eventRegistry.GetLatestVersion(eventTypeName);
            
            var contract = _eventRegistry.ConvertFromDomainEvent(domainEvent, version);
            var json = JsonSerializer.Serialize(contract);
            
            var isValid = _validator.ValidateMessage(eventTypeName, json, version);
            
            return new ValidationResult(isValid, isValid ? null : $"Event {eventTypeName} v{version} failed schema validation");
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Validation error: {ex.Message}");
        }
    }

    public ValidationResult ValidateMessage(string messageType, string jsonMessage)
    {
        try
        {
            var isValid = _validator.ValidateMessage(messageType, jsonMessage);
            return new ValidationResult(isValid, isValid ? null : $"Message {messageType} failed schema validation");
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Validation error: {ex.Message}");
        }
    }

    public ValidationResult ValidateOutput(DepartmentalOutputBase output)
    {
        try
        {
            var outputType = output.GetType().Name;
            var json = JsonSerializer.Serialize(output);
            
            var isValid = _validator.ValidateMessage(outputType, json);
            return new ValidationResult(isValid, isValid ? null : $"Output {outputType} failed schema validation");
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
            var json = JsonSerializer.Serialize(contract);
            var isValid = _validator.ValidateMessage(contract.EventType, json, contract.Version);
            
            return new ValidationResult(isValid, isValid ? null : $"Event contract {contract.EventType} v{contract.Version} failed schema validation");
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Validation error: {ex.Message}");
        }
    }

    public IEnumerable<string> GetSupportedEventTypes()
    {
        return new[] { "TradeCreated", "TradeStatusChanged", "TradeUpdated", "TradeEnriched", "TradeValidationFailed" };
    }

    public IEnumerable<int> GetSupportedVersions(string eventType)
    {
        return _eventRegistry.GetSupportedVersions(eventType);
    }

    public IEnumerable<string> GetSupportedMessageTypes()
    {
        return new[] { "TradeMessage", "EquityTradeMessage", "OptionTradeMessage", "FxTradeMessage", "TradeMessageEnvelope" };
    }

    public IEnumerable<string> GetSupportedOutputTypes()
    {
        return new[] { "ComplianceOutput", "RiskManagementOutput", "ReportingOutput" };
    }
}

public record ValidationResult(bool IsValid, string? ErrorMessage)
{
    public static ValidationResult Success() => new(true, null);
    public static ValidationResult Failure(string errorMessage) => new(false, errorMessage);
}