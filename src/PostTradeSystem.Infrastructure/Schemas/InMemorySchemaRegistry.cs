using PostTradeSystem.Core.Schemas;
using System.Collections.Concurrent;

namespace PostTradeSystem.Infrastructure.Schemas;

public class InMemorySchemaRegistry : ISchemaRegistry
{
    private readonly ConcurrentDictionary<string, List<SchemaVersion>> _schemas = new();
    private int _nextId = 1;

    public Task<string> GetSchemaAsync(string subject, int version)
    {
        if (_schemas.TryGetValue(subject, out var versions))
        {
            var schema = versions.FirstOrDefault(v => v.Version == version);
            if (schema != null)
            {
                return Task.FromResult(schema.Schema);
            }
        }
        
        throw new ArgumentException($"Schema not found for subject '{subject}' version {version}");
    }

    public Task<int> RegisterSchemaAsync(string subject, string schema, int version)
    {
        var versions = _schemas.GetOrAdd(subject, _ => new List<SchemaVersion>());
        
        var existingSchema = versions.FirstOrDefault(v => v.Schema == schema);
        if (existingSchema != null)
        {
            return Task.FromResult(existingSchema.Id);
        }

        var newVersion = new SchemaVersion
        {
            Id = Interlocked.Increment(ref _nextId),
            Version = version,
            Schema = schema,
            Subject = subject,
            Timestamp = DateTime.UtcNow
        };

        versions.Add(newVersion);
        return Task.FromResult(newVersion.Id);
    }

    public Task<int> RegisterSchemaAsync(string subject, string schema)
    {
        // Default to version 1 for backward compatibility
        return RegisterSchemaAsync(subject, schema, 1);
    }

    public Task<bool> IsCompatibleAsync(string subject, string schema)
    {
        if (!_schemas.TryGetValue(subject, out var versions) || versions.Count == 0)
        {
            return Task.FromResult(true);
        }

        var latestSchema = versions.Last().Schema;
        var isCompatible = CheckBackwardCompatibility(latestSchema, schema);
        return Task.FromResult(isCompatible);
    }

    public Task<SchemaMetadata> GetLatestSchemaAsync(string subject)
    {
        if (_schemas.TryGetValue(subject, out var versions) && versions.Count > 0)
        {
            var latest = versions.Last();
            return Task.FromResult(new SchemaMetadata(latest.Id, latest.Version, latest.Schema, latest.Subject));
        }
        
        throw new ArgumentException($"No schemas found for subject '{subject}'");
    }

    public Task<IEnumerable<string>> GetSubjectsAsync()
    {
        return Task.FromResult(_schemas.Keys.AsEnumerable());
    }

    private static bool CheckBackwardCompatibility(string oldSchema, string newSchema)
    {
        return true;
    }


    private class SchemaVersion
    {
        public int Id { get; set; }
        public int Version { get; set; }
        public string Schema { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}