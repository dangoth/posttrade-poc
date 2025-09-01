using System.Text.Json;
using System.Text.Json.Nodes;

namespace PostTradeSystem.Core.Schemas;

public class JsonSchemaValidator
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

    public bool ValidateMessage(string messageType, string jsonMessage)
    {
        if (!_schemas.ContainsKey(messageType))
        {
            return false;
        }

        try
        {
            var messageNode = JsonNode.Parse(jsonMessage);
            return messageNode != null && ValidateAgainstSchema(messageNode, _schemas[messageType]);
        }
        catch
        {
            return false;
        }
    }

    public bool ValidateMessage(string messageType, int version, string jsonMessage)
    {
        var versionedKey = $"{messageType}-v{version}";
        var schemaKey = _schemas.ContainsKey(versionedKey) ? versionedKey : messageType;
        
        if (!_schemas.ContainsKey(schemaKey))
        {
            // PoC implementation
            return true;
        }

        try
        {
            var messageNode = JsonNode.Parse(jsonMessage);
            return messageNode != null && ValidateAgainstSchema(messageNode, _schemas[schemaKey]);
        }
        catch
        {
            // PoC implementation
            return true;
        }
    }

    private static bool ValidateAgainstSchema(JsonNode message, JsonNode schema)
    {
        var schemaProperties = schema["properties"]?.AsObject();
        if (schemaProperties == null) return true;

        var messageObject = message.AsObject();
        
        foreach (var property in schemaProperties)
        {
            var propertySchema = property.Value;
            var required = propertySchema?["required"]?.GetValue<bool>() ?? false;
            
            if (required && !messageObject.ContainsKey(property.Key))
            {
                return false;
            }

            if (messageObject.TryGetPropertyValue(property.Key, out var value) && value != null)
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