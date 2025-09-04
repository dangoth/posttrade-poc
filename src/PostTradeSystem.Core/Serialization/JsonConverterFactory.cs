using System.Text.Json;
using System.Text.Json.Serialization;

namespace PostTradeSystem.Core.Serialization;
public static class JsonConverterFactory
{
    /// <summary>
    /// Creates standard JSON serializer options for event serialization
    /// </summary>
    public static JsonSerializerOptions CreateEventSerializationOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(),
                new DateTimeConverter(),
                new DecimalConverter(),
                new DictionaryStringObjectConverter()
            }
        };
    }

    /// <summary>
    /// Creates standard JSON serializer options for message serialization
    /// </summary>
    public static JsonSerializerOptions CreateMessageSerializationOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Creates JSON serializer options with custom converters
    /// </summary>
    public static JsonSerializerOptions CreateCustomOptions(params JsonConverter[] additionalConverters)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Add standard converters
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new DateTimeConverter());
        options.Converters.Add(new DecimalConverter());

        // Add custom converters
        foreach (var converter in additionalConverters)
        {
            options.Converters.Add(converter);
        }

        return options;
    }
}