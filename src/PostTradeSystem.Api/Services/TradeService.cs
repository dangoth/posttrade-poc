using PostTradeSystem.Core.Messages;
using PostTradeSystem.Core.Common;
using PostTradeSystem.Infrastructure.Kafka;

namespace PostTradeSystem.Api.Services;

public class TradeService
{
    private readonly IKafkaProducerService _producer;
    private readonly IConfiguration _config;

    public TradeService(IKafkaProducerService producer, IConfiguration config)
    {
        _producer = producer;
        _config = config;
    }

    public async Task<Result<object>> ProduceEquityTradeAsync(EquityTradeMessage trade)
    {
        return await ProduceTradeAsync(trade, "Kafka:Topics:EquityTrades", "trades.equities", "equity");
    }

    public async Task<Result<object>> ProduceOptionTradeAsync(OptionTradeMessage trade)
    {
        return await ProduceTradeAsync(trade, "Kafka:Topics:OptionTrades", "trades.options", "option");
    }

    public async Task<Result<object>> ProduceFxTradeAsync(FxTradeMessage trade)
    {
        return await ProduceTradeAsync(trade, "Kafka:Topics:FxTrades", "trades.fx", "FX");
    }

    private async Task<Result<object>> ProduceTradeAsync<T>(T trade, string topicConfigKey, string defaultTopic, string tradeType) 
        where T : TradeMessage
    {
        try
        {
            var topic = _config[topicConfigKey] ?? defaultTopic;
            var result = await _producer.ProduceAsync(topic, trade);
            
            if (result.IsSuccess)
            {
                var response = CreateSuccessResponse(trade.TradeId, result.Value!);
                return Result<object>.Success(response);
            }
            else
            {
                return Result<object>.Failure($"Failed to produce message: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            return Result<object>.Failure($"Failed to produce {tradeType} trade: {ex.Message}");
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