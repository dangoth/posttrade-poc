using System.Text.Json;
using System.Text.Json.Serialization;

namespace PostTradeSystem.Core.Serialization;

public static class JsonConverterFactory
{
    public static JsonSerializerOptions CreateEventSerializationOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { 
                new JsonStringEnumConverter(),
                new DictionaryObjectJsonConverter()
            }
        };
    }
    public static JsonSerializerOptions CreateMessageSerializationOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
}