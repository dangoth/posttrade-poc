using PostTradeSystem.Core.Messages;
using PostTradeSystem.Infrastructure.Kafka;

namespace PostTradeSystem.Api.Services;

public class TradeService
{
    private readonly KafkaProducerService _producer;
    private readonly IConfiguration _config;

    public TradeService(KafkaProducerService producer, IConfiguration config)
    {
        _producer = producer;
        _config = config;
    }

    public async Task<object> ProduceEquityTradeAsync(EquityTradeMessage trade)
    {
        return await ProduceTradeAsync(trade, "Kafka:Topics:EquityTrades", "trades.equities", "equity");
    }

    public async Task<object> ProduceOptionTradeAsync(OptionTradeMessage trade)
    {
        return await ProduceTradeAsync(trade, "Kafka:Topics:OptionTrades", "trades.options", "option");
    }

    public async Task<object> ProduceFxTradeAsync(FxTradeMessage trade)
    {
        return await ProduceTradeAsync(trade, "Kafka:Topics:FxTrades", "trades.fx", "FX");
    }

    private async Task<object> ProduceTradeAsync<T>(T trade, string topicConfigKey, string defaultTopic, string tradeType) 
        where T : TradeMessage
    {
        try
        {
            var topic = _config[topicConfigKey] ?? defaultTopic;
            var result = await _producer.ProduceAsync(topic, trade);
            
            return CreateSuccessResponse(trade.TradeId, result);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to produce {tradeType} trade: {ex.Message}", ex);
        }
    }

    private static object CreateSuccessResponse(string messageId, Confluent.Kafka.DeliveryResult<string, string> result)
    {
        return new 
        { 
            Success = true, 
            MessageId = messageId,
            Topic = result.Topic,
            Partition = result.Partition.Value,
            Offset = result.Offset.Value
        };
    }
}