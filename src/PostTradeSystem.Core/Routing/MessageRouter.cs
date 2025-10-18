using PostTradeSystem.Core.Helpers;

namespace PostTradeSystem.Core.Routing;

public class MessageRouter : IMessageRouter
{
    private readonly Dictionary<string, string> _topicToSourceSystemMapping;

    public MessageRouter()
    {
        _topicToSourceSystemMapping = new Dictionary<string, string>
        {
            ["trades.equities"] = "EQUITY_SYSTEM",
            ["trades.fx"] = "FX_SYSTEM",
            ["trades.options"] = "OPTION_SYSTEM"
        };
    }

    public string DetermineSourceSystem(string topic, Dictionary<string, string>? headers)
    {
        if (headers?.TryGetValue("sourceSystem", out var headerSourceSystem) == true && 
            !string.IsNullOrEmpty(headerSourceSystem))
        {
            return headerSourceSystem;
        }

        if (_topicToSourceSystemMapping.TryGetValue(topic, out var mappedSourceSystem))
        {
            return mappedSourceSystem;
        }

        return "UNKNOWN_SYSTEM";
    }

    public string GetPartitionKey(string messageType, string sourceSystem, string traderId, string instrumentId)
    {
        var baseKey = TradePropertyMapper.CreatePartitionKey(traderId, instrumentId);
        return $"{sourceSystem}:{baseKey}";
    }
}