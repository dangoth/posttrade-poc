namespace PostTradeSystem.Core.Models;

public abstract class TradeBase
{
    public string TradeId { get; set; } = string.Empty;
    public string TraderId { get; set; } = string.Empty;
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public TradeDirection Direction { get; set; }
    public DateTime TradeDateTime { get; set; }
    public string Currency { get; set; } = string.Empty;
    public TradeStatus Status { get; set; }
    public string CounterpartyId { get; set; } = string.Empty;
}

public enum TradeDirection
{
    Buy,
    Sell
}

public enum TradeStatus
{
    Pending,
    Executed,
    Settled,
    Failed
}