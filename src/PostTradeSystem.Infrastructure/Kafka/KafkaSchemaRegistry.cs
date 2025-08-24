using PostTradeSystem.Core.Schemas;

namespace PostTradeSystem.Infrastructure.Kafka;

public class KafkaSchemaRegistry
{
    private readonly ISchemaRegistry _schemaRegistry;
    private readonly JsonSchemaValidator _validator;

    public KafkaSchemaRegistry(ISchemaRegistry schemaRegistry)
    {
        _schemaRegistry = schemaRegistry;
        _validator = new JsonSchemaValidator();
        InitializeSchemas();
    }

    public async Task<bool> ValidateMessageAsync(string messageType, string jsonMessage)
    {
        try
        {
            var subject = GetSubjectName(messageType);
            var latestSchema = await _schemaRegistry.GetLatestSchemaAsync(subject);
            return _validator.ValidateMessage(messageType, jsonMessage);
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> RegisterMessageSchemaAsync(string messageType, string schema)
    {
        var subject = GetSubjectName(messageType);
        return await _schemaRegistry.RegisterSchemaAsync(subject, schema);
    }

    public async Task<bool> IsSchemaCompatibleAsync(string messageType, string schema)
    {
        var subject = GetSubjectName(messageType);
        return await _schemaRegistry.IsCompatibleAsync(subject, schema);
    }

    private void InitializeSchemas()
    {
        _validator.RegisterSchema("EQUITY", MessageSchemas.EquityTradeMessageSchema);
        _validator.RegisterSchema("OPTION", MessageSchemas.OptionTradeMessageSchema);
        _validator.RegisterSchema("FX", MessageSchemas.FxTradeMessageSchema);
        _validator.RegisterSchema("ENVELOPE", MessageSchemas.TradeMessageEnvelopeSchema);
    }

    private static string GetSubjectName(string messageType)
    {
        return $"trade-{messageType.ToLower()}-value";
    }
}