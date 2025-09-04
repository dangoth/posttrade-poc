using System.Text;

namespace PostTradeSystem.Infrastructure.Kafka;

public static class KafkaHeaderUtility
{
    public static Dictionary<string, byte[]> CreateStandardHeaders(string messageType, string version = "1.0")
    {
        return new Dictionary<string, byte[]>
        {
            ["messageType"] = Encoding.UTF8.GetBytes(messageType),
            ["version"] = Encoding.UTF8.GetBytes(version),
            ["timestamp"] = Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString())
        };
    }

    public static Dictionary<string, byte[]> CreateEventHeaders(
        string eventType, 
        int version, 
        string schemaId, 
        DateTime serializedAt, 
        string correlationId, 
        string causedBy, 
        long aggregateVersion,
        Dictionary<string, string>? metadata = null)
    {
        var headers = new Dictionary<string, byte[]>
        {
            ["event-type"] = Encoding.UTF8.GetBytes(eventType),
            ["event-version"] = Encoding.UTF8.GetBytes(version.ToString()),
            ["schema-id"] = Encoding.UTF8.GetBytes(schemaId),
            ["serialized-at"] = Encoding.UTF8.GetBytes(serializedAt.ToString("O")),
            ["correlation-id"] = Encoding.UTF8.GetBytes(correlationId),
            ["caused-by"] = Encoding.UTF8.GetBytes(causedBy),
            ["aggregate-version"] = Encoding.UTF8.GetBytes(aggregateVersion.ToString())
        };

        if (metadata != null)
        {
            foreach (var kvp in metadata)
            {
                headers[$"meta-{kvp.Key}"] = Encoding.UTF8.GetBytes(kvp.Value);
            }
        }

        return headers;
    }

    public static string GetHeaderValue(Dictionary<string, byte[]> headers, string key)
    {
        return headers.TryGetValue(key, out var value) 
            ? Encoding.UTF8.GetString(value) 
            : throw new ArgumentException($"Header '{key}' not found");
    }

    public static string? GetHeaderValueOrDefault(Dictionary<string, byte[]> headers, string key, string? defaultValue = null)
    {
        return headers.TryGetValue(key, out var value) 
            ? Encoding.UTF8.GetString(value) 
            : defaultValue;
    }

    public static bool TryGetHeaderValue(Dictionary<string, byte[]> headers, string key, out string value)
    {
        if (headers.TryGetValue(key, out var bytes))
        {
            value = Encoding.UTF8.GetString(bytes);
            return true;
        }
        
        value = string.Empty;
        return false;
    }

    public static Dictionary<string, string> ExtractMetadataHeaders(Dictionary<string, byte[]> headers)
    {
        var metadata = new Dictionary<string, string>();
        
        foreach (var header in headers.Where(h => h.Key.StartsWith("meta-")))
        {
            var key = header.Key[5..]; // Remove "meta-" prefix
            metadata[key] = Encoding.UTF8.GetString(header.Value);
        }
        
        return metadata;
    }

    public static bool HasRequiredHeaders(Dictionary<string, byte[]> headers, params string[] requiredHeaders)
    {
        return requiredHeaders.All(headers.ContainsKey);
    }
}