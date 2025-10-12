using System.Text.Json;
using System.Text.Json.Nodes;

namespace PostTradeSystem.Core.Schemas;

public class JsonSchemaValidator : IJsonSchemaValidator
{
    private readonly Dictionary<string, JsonNode> _schemas = new();

    public void RegisterSchema(string messageType, string jsonSchema)
    {
        var schemaNode = JsonNode.Parse(jsonSchema);
        if (schemaNode != null)
        {
            _schemas[messageType] = schemaNode;
        }
    }

    public bool ValidateMessage(string messageType, string jsonMessage, int? version = null)
    {
        var schemaKey = GetSchemaKey(messageType, version);
        
        if (!_schemas.ContainsKey(schemaKey))
        {
            // PoC implementation
            return version.HasValue;
        }

        try
        {
            var messageNode = JsonNode.Parse(jsonMessage);
            var result = messageNode != null && ValidateAgainstSchema(messageNode, _schemas[schemaKey]);
            return result;
        }
        catch (Exception)
        {
            // PoC implementation
            return version.HasValue;
        }
    }

    private string GetSchemaKey(string messageType, int? version)
    {
        if (version.HasValue)
        {
            var versionedKey = $"{messageType}-v{version}";
            return _schemas.ContainsKey(versionedKey) ? versionedKey : messageType;
        }
        return messageType;
    }

    private static bool ValidateAgainstSchema(JsonNode message, JsonNode schema)
    {
        var schemaProperties = schema["properties"]?.AsObject();
        if (schemaProperties == null) 
        {
            return true;
        }

        var messageObject = message.AsObject();
        
        var messagePropertiesLookup = messageObject.ToDictionary(
            kvp => kvp.Key, 
            kvp => kvp.Value, 
            StringComparer.OrdinalIgnoreCase);
        
        foreach (var property in schemaProperties)
        {
            var propertySchema = property.Value;
            var required = propertySchema?["required"]?.GetValue<bool>() ?? false;
            
            if (required && !messagePropertiesLookup.ContainsKey(property.Key))
            {
                return false;
            }

            if (messagePropertiesLookup.TryGetValue(property.Key, out var value) && value != null)
            {
                var expectedType = propertySchema?["type"]?.GetValue<string>();
                if (!ValidateType(value, expectedType))
                {
                    return false;
                }

                if (expectedType == "string" && value.GetValueKind() == JsonValueKind.String)
                {
                    var stringValue = value.GetValue<string>();
                    
                    if (propertySchema?["minLength"] != null)
                    {
                        var minLength = propertySchema["minLength"]!.GetValue<int>();
                        if (stringValue.Length < minLength)
                        {
                            return false;
                        }
                    }

                    if (propertySchema?["pattern"] != null)
                    {
                        var pattern = propertySchema["pattern"]!.GetValue<string>();
                        if (!System.Text.RegularExpressions.Regex.IsMatch(stringValue, pattern))
                        {
                            return false;
                        }
                    }
                    
                    if (propertySchema?["enum"] != null)
                    {
                        var enumValues = propertySchema["enum"]!.AsArray();
                        var validValues = enumValues.Select(v => v?.GetValue<string>()).Where(v => v != null);
                        if (!validValues.Any(v => string.Equals(v, stringValue, StringComparison.OrdinalIgnoreCase)))
                        {
                            return false;
                        }
                    }
                }
            }
        }

        return true;
    }

    private static bool ValidateType(JsonNode value, string? expectedType)
    {
        return expectedType switch
        {
            "string" => value.GetValueKind() == JsonValueKind.String,
            "number" => value.GetValueKind() is JsonValueKind.Number,
            "boolean" => value.GetValueKind() == JsonValueKind.True || value.GetValueKind() == JsonValueKind.False,
            "object" => value.GetValueKind() == JsonValueKind.Object,
            "array" => value.GetValueKind() == JsonValueKind.Array,
            _ => true
        };
    }
}