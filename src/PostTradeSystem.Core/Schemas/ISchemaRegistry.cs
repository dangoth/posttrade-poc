namespace PostTradeSystem.Core.Schemas;

public interface ISchemaRegistry
{
    Task<string> GetSchemaAsync(string subject, int version);
    Task<int> RegisterSchemaAsync(string subject, string schema);
    Task<bool> IsCompatibleAsync(string subject, string schema);
    Task<SchemaMetadata> GetLatestSchemaAsync(string subject);
    Task<IEnumerable<string>> GetSubjectsAsync();
}

public record SchemaMetadata(int Id, int Version, string Schema, string Subject);