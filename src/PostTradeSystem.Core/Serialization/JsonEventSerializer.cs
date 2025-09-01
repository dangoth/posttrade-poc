using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Schemas;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PostTradeSystem.Core.Serialization;

public class JsonEventSerializer : IEventSerializer
{
    private readonly EventSerializationRegistry _registry;
    private readonly ISchemaRegistry _schemaRegistry;
    private readonly JsonSchemaValidator _validator;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonEventSerializer(EventSerializationRegistry registry, ISchemaRegistry schemaRegistry, JsonSchemaValidator validator)
    {
        _registry = registry;
        _schemaRegistry = schemaRegistry;
        _validator = validator;
        _jsonOptions = CreateJsonOptions();
    }

    public async Task<SerializedEvent> Serialize(IDomainEvent domainEvent)
    {
        var eventType = GetEventTypeName(domainEvent);
        var latestVersion = _registry.GetLatestVersion(eventType);
        
        var contract = _registry.ConvertFromDomainEvent(domainEvent, latestVersion);
        var jsonData = JsonSerializer.Serialize(contract, contract.GetType(), _jsonOptions);
        var schemaId = await GetSchemaId(eventType, latestVersion);
        
        return new SerializedEvent(
            EventType: eventType,
            Version: latestVersion,
            Data: jsonData,
            SchemaId: schemaId,
            SerializedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, string>
            {
                ["serializer"] = "JsonEventSerializer",
                ["domainEventType"] = domainEvent.GetType().FullName!,
                ["contractType"] = contract.GetType().FullName!
            });
    }

    public IDomainEvent Deserialize(SerializedEvent serializedEvent)
    {
        var contractType = _registry.GetContractType(serializedEvent.EventType, serializedEvent.Version);
        if (contractType == null)
        {
            throw new InvalidOperationException(
                $"No contract registered for event type '{serializedEvent.EventType}' version {serializedEvent.Version}");
        }

        ValidateAgainstSchema(serializedEvent);

        var contract = JsonSerializer.Deserialize(serializedEvent.Data, contractType, _jsonOptions) as IVersionedEventContract;
        if (contract == null)
        {
            throw new InvalidOperationException($"Failed to deserialize event data to contract type {contractType.Name}");
        }

        var latestVersion = _registry.GetLatestVersion(serializedEvent.EventType);
        if (contract.Version != latestVersion)
        {
            contract = ConvertToLatestVersion(contract, latestVersion);
        }

        return _registry.ConvertToDomainEvent(contract);
    }

    public bool CanHandle(string eventType, int version)
    {
        return _registry.GetContractType(eventType, version) != null;
    }

    public IEnumerable<int> GetSupportedVersions(string eventType)
    {
        return _registry.GetSupportedVersions(eventType);
    }

    private static string GetEventTypeName(IDomainEvent domainEvent)
    {
        var typeName = domainEvent.GetType().Name;
        return typeName.EndsWith("Event") ? typeName[..^5] : typeName;
    }

    private async Task<string> GetSchemaId(string eventType, int version)
    {
        try
        {
            var subject = $"event-{eventType.ToLower()}-v{version}";
            var schema = await _schemaRegistry.GetLatestSchemaAsync(subject);
            return schema.Id.ToString();
        }
        catch
        {
            return "unknown";
        }
    }

    private void ValidateAgainstSchema(SerializedEvent serializedEvent)
    {
        try
        {
            var isValid = _validator.ValidateMessage(serializedEvent.EventType, serializedEvent.Version, serializedEvent.Data);
            if (!isValid)
            {
                // PoC implementation
                System.Diagnostics.Debug.WriteLine($"Schema validation failed for type '{serializedEvent.EventType}' version {serializedEvent.Version}");
                System.Diagnostics.Debug.WriteLine($"Data: {serializedEvent.Data}");
            }
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            // PoC implementation
            System.Diagnostics.Debug.WriteLine($"Schema validation exception: {ex.Message}");
        }
    }

    private IVersionedEventContract ConvertToLatestVersion(IVersionedEventContract contract, int targetVersion)
    {
        if (contract.Version == targetVersion)
        {
            return contract;
        }

        var currentVersion = contract.Version;
        var current = contract;

        while (currentVersion < targetVersion)
        {
            var nextVersion = currentVersion + 1;
            current = ConvertToNextVersion(current, nextVersion);
            currentVersion = nextVersion;
        }

        return current;
    }

    private IVersionedEventContract ConvertToNextVersion(IVersionedEventContract contract, int targetVersion)
    {
        var eventType = contract.EventType;
        
        if (eventType == "TradeCreated")
        {
            if (contract.Version == 1 && targetVersion == 2)
            {
                var converter = new Contracts.TradeCreatedEventV1ToV2Converter();
                return converter.Convert((Contracts.TradeCreatedEventV1)contract);
            }
        }
        
        if (eventType == "TradeStatusChanged")
        {
            if (contract.Version == 1 && targetVersion == 2)
            {
                var converter = new Contracts.TradeStatusChangedEventV1ToV2Converter();
                return converter.Convert((Contracts.TradeStatusChangedEventV1)contract);
            }
        }

        throw new InvalidOperationException($"No converter available for {eventType} from version {contract.Version} to {targetVersion}");
    }

    private static JsonSerializerOptions CreateJsonOptions()
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
}

public class DateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateString = reader.GetString()!;
        if (DateTime.TryParse(dateString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var result))
        {
            return result.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(result, DateTimeKind.Utc) : result.ToUniversalTime();
        }
        return DateTime.Parse(dateString).ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
    }
}

public class DecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetDecimal();
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

public class DictionaryStringObjectConverter : JsonConverter<Dictionary<string, object>>
{
    public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dictionary = new Dictionary<string, object>();
        
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

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

            var propertyName = reader.GetString()!;
            reader.Read();

            var value = ReadValue(ref reader);
            dictionary[propertyName] = value;
        }

        throw new JsonException("Unexpected end of JSON");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        
        foreach (var kvp in value)
        {
            writer.WritePropertyName(kvp.Key);
            WriteValue(writer, kvp.Value);
        }
        
        writer.WriteEndObject();
    }

    private static object ReadValue(ref Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString()!,
            JsonTokenType.Number => reader.TryGetInt64(out var longValue) ? longValue : reader.GetDouble(),
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null!,
            JsonTokenType.StartObject => ReadObject(ref reader),
            JsonTokenType.StartArray => ReadArray(ref reader),
            _ => throw new JsonException($"Unsupported token type: {reader.TokenType}")
        };
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

            var propertyName = reader.GetString()!;
            reader.Read();
            obj[propertyName] = ReadValue(ref reader);
        }

        throw new JsonException("Unexpected end of JSON");
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

            array.Add(ReadValue(ref reader));
        }

        throw new JsonException("Unexpected end of JSON");
    }

    private static void WriteValue(Utf8JsonWriter writer, object value)
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
            case double doubleValue:
                writer.WriteNumberValue(doubleValue);
                break;
            case decimal decimalValue:
                writer.WriteNumberValue(decimalValue);
                break;
            case bool boolValue:
                writer.WriteBooleanValue(boolValue);
                break;
            case Dictionary<string, object> dictValue:
                writer.WriteStartObject();
                foreach (var kvp in dictValue)
                {
                    writer.WritePropertyName(kvp.Key);
                    WriteValue(writer, kvp.Value);
                }
                writer.WriteEndObject();
                break;
            case List<object> listValue:
                writer.WriteStartArray();
                foreach (var item in listValue)
                {
                    WriteValue(writer, item);
                }
                writer.WriteEndArray();
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}