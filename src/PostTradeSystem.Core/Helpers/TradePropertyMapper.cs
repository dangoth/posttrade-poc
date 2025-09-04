using PostTradeSystem.Core.Models;
using PostTradeSystem.Core.Messages;

namespace PostTradeSystem.Core.Helpers;

public static class TradePropertyMapper
{
    /// <summary>
    /// Maps base trade properties from TradeBase to TradeMessage
    /// </summary>
    public static void MapBaseTradeProperties(TradeBase trade, TradeMessage message)
    {
        message.TradeId = trade.TradeId;
        message.TraderId = trade.TraderId;
        message.InstrumentId = trade.InstrumentId;
        message.Quantity = trade.Quantity;
        message.Price = trade.Price;
        message.Direction = trade.Direction.ToString();
        message.TradeDateTime = trade.TradeDateTime;
        message.Currency = trade.Currency;
        message.Status = trade.Status.ToString();
        message.CounterpartyId = trade.CounterpartyId;
    }

    /// <summary>
    /// Maps base trade properties from TradeMessage to TradeBase
    /// </summary>
    public static void MapBaseTradeProperties(TradeMessage message, TradeBase trade)
    {
        trade.TradeId = message.TradeId;
        trade.TraderId = message.TraderId;
        trade.InstrumentId = message.InstrumentId;
        trade.Quantity = message.Quantity;
        trade.Price = message.Price;
        trade.Direction = Enum.Parse<TradeDirection>(message.Direction);
        trade.TradeDateTime = message.TradeDateTime;
        trade.Currency = message.Currency;
        trade.Status = Enum.Parse<TradeStatus>(message.Status);
        trade.CounterpartyId = message.CounterpartyId;
    }

    /// <summary>
    /// Creates a partition key for trade-related messages
    /// </summary>
    public static string CreatePartitionKey(string traderId, string instrumentId)
    {
        return $"{traderId}:{instrumentId}";
    }
}