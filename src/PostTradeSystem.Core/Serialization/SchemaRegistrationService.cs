using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Serialization.Contracts;
using System.Text.Json;

namespace PostTradeSystem.Core.Serialization;

public class SchemaRegistrationService
{
    private readonly ISchemaRegistry _schemaRegistry;
    private readonly JsonSchemaValidator _validator;

    public SchemaRegistrationService(ISchemaRegistry schemaRegistry, JsonSchemaValidator validator)
    {
        _schemaRegistry = schemaRegistry;
        _validator = validator;
    }

    public async Task RegisterEventContractSchemasAsync()
    {
        await RegisterTradeCreatedEventSchemasAsync();
        await RegisterTradeStatusChangedEventSchemasAsync();
        await RegisterTradeUpdatedEventSchemasAsync();
        await RegisterTradeEnrichedEventSchemasAsync();
        await RegisterTradeValidationFailedEventSchemasAsync();
    }

    private async Task RegisterTradeCreatedEventSchemasAsync()
    {
        var v1Schema = GenerateSchemaForContract(typeof(TradeCreatedEventV1));
        var v2Schema = GenerateSchemaForContract(typeof(TradeCreatedEventV2));

        await _schemaRegistry.RegisterSchemaAsync("event-tradecreated-v1", v1Schema);
        await _schemaRegistry.RegisterSchemaAsync("event-tradecreated-v2", v2Schema);

        _validator.RegisterSchema("TradeCreated-v1", v1Schema);
        _validator.RegisterSchema("TradeCreated-v2", v2Schema);
        _validator.RegisterSchema("TradeCreated", v2Schema);
    }

    private async Task RegisterTradeStatusChangedEventSchemasAsync()
    {
        var v1Schema = GenerateSchemaForContract(typeof(TradeStatusChangedEventV1));
        var v2Schema = GenerateSchemaForContract(typeof(TradeStatusChangedEventV2));

        await _schemaRegistry.RegisterSchemaAsync("event-tradestatuschanged-v1", v1Schema);
        await _schemaRegistry.RegisterSchemaAsync("event-tradestatuschanged-v2", v2Schema);

        _validator.RegisterSchema("TradeStatusChanged-v1", v1Schema);
        _validator.RegisterSchema("TradeStatusChanged-v2", v2Schema);
        _validator.RegisterSchema("TradeStatusChanged", v2Schema);
    }

    private async Task RegisterTradeUpdatedEventSchemasAsync()
    {
        var v1Schema = GenerateSchemaForContract(typeof(TradeUpdatedEventV1));

        await _schemaRegistry.RegisterSchemaAsync("event-tradeupdated-v1", v1Schema);
        _validator.RegisterSchema("TradeUpdated-v1", v1Schema);
        _validator.RegisterSchema("TradeUpdated", v1Schema);
    }

    private async Task RegisterTradeEnrichedEventSchemasAsync()
    {
        var v1Schema = GenerateSchemaForContract(typeof(TradeEnrichedEventV1));

        await _schemaRegistry.RegisterSchemaAsync("event-tradeenriched-v1", v1Schema);
        _validator.RegisterSchema("TradeEnriched-v1", v1Schema);
        _validator.RegisterSchema("TradeEnriched", v1Schema);
    }

    private async Task RegisterTradeValidationFailedEventSchemasAsync()
    {
        var v1Schema = GenerateSchemaForContract(typeof(TradeValidationFailedEventV1));

        await _schemaRegistry.RegisterSchemaAsync("event-tradevalidationfailed-v1", v1Schema);
        _validator.RegisterSchema("TradeValidationFailed-v1", v1Schema);
        _validator.RegisterSchema("TradeValidationFailed", v1Schema);
    }

    private static string GenerateSchemaForContract(Type contractType)
    {
        var properties = contractType.GetProperties();
        var schemaProperties = new Dictionary<string, object>();

        foreach (var prop in properties)
        {
            var propType = GetJsonSchemaType(prop.PropertyType);
            var isRequired = !prop.PropertyType.IsGenericType || 
                           prop.PropertyType.GetGenericTypeDefinition() != typeof(Nullable<>);

            var propertySchema = new Dictionary<string, object>
            {
                ["type"] = propType,
                ["required"] = isRequired
            };

            if (prop.Name == "RiskProfile" && contractType.Name.Contains("V2"))
            {
                propertySchema["minLength"] = 1;
                propertySchema["pattern"] = "^(STANDARD|HIGH_RISK|LOW_RISK)$";
            }

            schemaProperties[ToCamelCase(prop.Name)] = propertySchema;
        }

        var schema = new
        {
            type = "object",
            properties = schemaProperties
        };

        return JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string GetJsonSchemaType(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int) || type == typeof(long) || type == typeof(decimal) || type == typeof(double) || type == typeof(float)) return "number";
        if (type == typeof(bool)) return "boolean";
        if (type == typeof(DateTime)) return "string";
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) return "object";
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return "array";
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return GetJsonSchemaType(type.GetGenericArguments()[0]);
        }
        return "object";
    }

    private static string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input) || char.IsLower(input[0]))
            return input;

        return char.ToLower(input[0]) + input[1..];
    }
}