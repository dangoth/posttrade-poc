using System.Text.Json;
using System.Text.Json.Nodes;

namespace PostTradeSystem.Core.Schemas;

public class JsonSchemaValidator : IJsonSchemaValidator
{
    private readonly Dictionary<string, JsonNode> _schemas = new();

    public void RegisterSchema(string schemaName, string schemaJson)
    {
        try
        {
            var schema = JsonNode.Parse(schemaJson);
            if (schema != null)
            {
                _schemas[schemaName] = schema;
            }
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON schema for '{schemaName}': {ex.Message}", ex);
        }
    }

    public bool ValidateMessage(string messageType, string messageJson, int? version)
    {
        if (string.IsNullOrEmpty(messageJson))
            return false;

        if (!_schemas.TryGetValue(messageType, out var schema))
        {
            return version.HasValue;
        }

        try
        {
            var message = JsonDocument.Parse(messageJson);
            return ValidateAgainstSchema(message.RootElement, schema);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private bool ValidateAgainstSchema(JsonElement message, JsonNode schema)
    {
        var schemaType = schema["type"]?.ToString();
        if (schemaType != "object") return true;

        var schemaProperties = schema["properties"]?.AsObject();
        if (schemaProperties == null) return true;

        return ValidateObjectProperties(message, schemaProperties);
    }

    private bool ValidateObjectProperties(JsonElement jsonObject, JsonObject schemaProperties)
    {
        foreach (var schemaProperty in schemaProperties)
        {
            var propertyName = schemaProperty.Key;
            var propertySchema = schemaProperty.Value;

            var isRequired = propertySchema?["required"]?.GetValue<bool>() ?? false;

            if (jsonObject.TryGetProperty(propertyName, out var property))
            {
                if (!ValidateProperty(property, propertySchema!)) return false;
            }
            else if (isRequired)
            {
                return false;
            }
        }

        return true;
    }

    private bool ValidateProperty(JsonElement property, JsonNode propertySchema)
    {
        var propertyType = propertySchema["type"]?.ToString();
        if (propertyType == null) return true;

        if (!ValidateType(property, propertyType)) return false;

        // Validate enum if present
        var enumValues = propertySchema["enum"]?.AsArray();
        if (enumValues != null && propertyType == "string")
        {
            var stringValue = property.GetString();
            return enumValues.Any(e => string.Equals(e?.ToString(), stringValue, StringComparison.OrdinalIgnoreCase));
        }

        // Validate format if present
        var format = propertySchema["format"]?.ToString();
        if (format != null && propertyType == "string")
        {
            var stringValue = property.GetString();
            if (!ValidateFormat(stringValue, format)) return false;
        }

        // Recursively validate nested objects
        if (propertyType == "object" && property.ValueKind == JsonValueKind.Object)
        {
            var nestedProperties = propertySchema["properties"]?.AsObject();
            if (nestedProperties != null)
            {
                return ValidateObjectProperties(property, nestedProperties);
            }
        }

        return true;
    }

    private static bool ValidateType(JsonElement element, string expectedType)
    {
        return expectedType switch
        {
            "string" => element.ValueKind == JsonValueKind.String,
            "number" => element.ValueKind == JsonValueKind.Number,
            "integer" => element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out _),
            "boolean" => element.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "object" => element.ValueKind == JsonValueKind.Object,
            "array" => element.ValueKind == JsonValueKind.Array,
            _ => true
        };
    }

    private static bool ValidateFormat(string? value, string format)
    {
        if (value == null) return false;

        return format switch
        {
            "date-time" => DateTime.TryParse(value, out _),
            _ => true
        };
    }
}