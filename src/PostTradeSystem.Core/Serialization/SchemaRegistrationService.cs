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
        var eventRegistrations = new[]
        {
            new EventRegistration("TradeCreated", new[] { typeof(TradeCreatedEventV1), typeof(TradeCreatedEventV2) }),
            new EventRegistration("TradeStatusChanged", new[] { typeof(TradeStatusChangedEventV1), typeof(TradeStatusChangedEventV2) }),
            new EventRegistration("TradeUpdated", new[] { typeof(TradeUpdatedEventV1) }),
            new EventRegistration("TradeEnriched", new[] { typeof(TradeEnrichedEventV1) }),
            new EventRegistration("TradeValidationFailed", new[] { typeof(TradeValidationFailedEventV1) })
        };

        foreach (var registration in eventRegistrations)
        {
            await RegisterEventSchemasAsync(registration);
        }
    }

    private async Task RegisterEventSchemasAsync(EventRegistration registration)
    {
        var schemas = new List<(int version, string schema)>();
        
        for (int i = 0; i < registration.ContractTypes.Length; i++)
        {
            var version = i + 1;
            var schema = GenerateSchemaForContract(registration.ContractTypes[i]);
            schemas.Add((version, schema));
            
            var subjectName = $"event-{registration.EventName.ToLower()}-v{version}";
            await _schemaRegistry.RegisterSchemaAsync(subjectName, schema);
            
            _validator.RegisterSchema($"{registration.EventName}-v{version}", schema);
        }
        
        // Register the latest version as the default
        if (schemas.Count > 0)
        {
            var latestSchema = schemas.Last().schema;
            _validator.RegisterSchema(registration.EventName, latestSchema);
        }
    }

    private record EventRegistration(string EventName, Type[] ContractTypes);

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