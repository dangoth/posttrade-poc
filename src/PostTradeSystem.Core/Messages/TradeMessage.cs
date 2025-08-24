namespace PostTradeSystem.Core.Messages;

public abstract class TradeMessage
{
    public string TradeId { get; set; } = string.Empty;
    public string TraderId { get; set; } = string.Empty;
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public string Direction { get; set; } = string.Empty;
    public DateTime TradeDateTime { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CounterpartyId { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    
    public virtual string GetPartitionKey()
    {
        return $"{TraderId}:{InstrumentId}";
    }
}