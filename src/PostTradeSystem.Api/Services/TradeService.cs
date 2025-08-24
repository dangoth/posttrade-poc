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
        try
        {
            var topic = _config["Kafka:Topics:EquityTrades"] ?? "trades.equities";
            var result = await _producer.ProduceAsync(topic, trade);
            
            return new 
            { 
                Success = true, 
                MessageId = trade.TradeId,
                Topic = result.Topic,
                Partition = result.Partition.Value,
                Offset = result.Offset.Value
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to produce equity trade: {ex.Message}", ex);
        }
    }

    public async Task<object> ProduceOptionTradeAsync(OptionTradeMessage trade)
    {
        try
        {
            var topic = _config["Kafka:Topics:OptionTrades"] ?? "trades.options";
            var result = await _producer.ProduceAsync(topic, trade);
            
            return new 
            { 
                Success = true, 
                MessageId = trade.TradeId,
                Topic = result.Topic,
                Partition = result.Partition.Value,
                Offset = result.Offset.Value
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to produce option trade: {ex.Message}", ex);
        }
    }

    public async Task<object> ProduceFxTradeAsync(FxTradeMessage trade)
    {
        try
        {
            var topic = _config["Kafka:Topics:FxTrades"] ?? "trades.fx";
            var result = await _producer.ProduceAsync(topic, trade);
            
            return new 
            { 
                Success = true, 
                MessageId = trade.TradeId,
                Topic = result.Topic,
                Partition = result.Partition.Value,
                Offset = result.Offset.Value
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to produce FX trade: {ex.Message}", ex);
        }
    }
}