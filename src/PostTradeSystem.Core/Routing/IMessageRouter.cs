namespace PostTradeSystem.Core.Routing;

public interface IMessageRouter
{
    string DetermineSourceSystem(string topic, Dictionary<string, string>? headers);
    string GetPartitionKey(string messageType, string sourceSystem, string traderId, string instrumentId);
}