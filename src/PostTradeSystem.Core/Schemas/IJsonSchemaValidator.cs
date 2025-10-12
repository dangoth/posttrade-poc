namespace PostTradeSystem.Core.Schemas;

public interface IJsonSchemaValidator
{
    void RegisterSchema(string messageType, string jsonSchema);
    bool ValidateMessage(string messageType, string jsonMessage, int? version = null);
}