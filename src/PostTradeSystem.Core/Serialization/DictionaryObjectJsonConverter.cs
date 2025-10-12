using System.Text.Json;
using System.Text.Json.Serialization;

namespace PostTradeSystem.Core.Serialization;

public class DictionaryObjectJsonConverter : JsonConverter<Dictionary<string, object>>
{
    public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        var dictionary = new Dictionary<string, object>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dictionary;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token");
            }

            string key = reader.GetString()!;
            reader.Read();

            object value = ReadValue(ref reader);
            dictionary[key] = value;
        }

        throw new JsonException("Unexpected end of JSON input");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            writer.WritePropertyName(kvp.Key);
            WriteValue(writer, kvp.Value, options);
        }

        writer.WriteEndObject();
    }

    private static object ReadValue(ref Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString()!,
            JsonTokenType.Number => ReadNumber(ref reader),
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null!,
            JsonTokenType.StartObject => ReadObject(ref reader),
            JsonTokenType.StartArray => ReadArray(ref reader),
            _ => throw new JsonException($"Unexpected token type: {reader.TokenType}")
        };
    }

    private static object ReadNumber(ref Utf8JsonReader reader)
    {
        // Try to preserve the original numeric type
        // For integers, try int first, then long
        if (reader.TryGetInt32(out int intValue))
        {
            return intValue;
        }
        if (reader.TryGetInt64(out long longValue))
        {
            return longValue;
        }
        
        // For floating-point numbers, try double first
        // then decimal for higher precision scenarios
        if (reader.TryGetDouble(out double doubleValue))
        {
            return doubleValue;
        }
        if (reader.TryGetDecimal(out decimal decimalValue))
        {
            return decimalValue;
        }
        
        throw new JsonException("Unable to parse number");
    }

    private static Dictionary<string, object> ReadObject(ref Utf8JsonReader reader)
    {
        var obj = new Dictionary<string, object>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return obj;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token");
            }

            string key = reader.GetString()!;
            reader.Read();
            object value = ReadValue(ref reader);
            obj[key] = value;
        }

        throw new JsonException("Unexpected end of JSON input");
    }

    private static List<object> ReadArray(ref Utf8JsonReader reader)
    {
        var array = new List<object>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return array;
            }

            object value = ReadValue(ref reader);
            array.Add(value);
        }

        throw new JsonException("Unexpected end of JSON input");
    }

    private static void WriteValue(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string stringValue:
                writer.WriteStringValue(stringValue);
                break;
            case int intValue:
                writer.WriteNumberValue(intValue);
                break;
            case long longValue:
                writer.WriteNumberValue(longValue);
                break;
            case decimal decimalValue:
                writer.WriteNumberValue(decimalValue);
                break;
            case double doubleValue:
                writer.WriteNumberValue(doubleValue);
                break;
            case float floatValue:
                writer.WriteNumberValue(floatValue);
                break;
            case bool boolValue:
                writer.WriteBooleanValue(boolValue);
                break;
            case Dictionary<string, object> dictValue:
                JsonSerializer.Serialize(writer, dictValue, options);
                break;
            case List<object> listValue:
                writer.WriteStartArray();
                foreach (var item in listValue)
                {
                    WriteValue(writer, item, options);
                }
                writer.WriteEndArray();
                break;
            default:
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
                break;
        }
    }
}