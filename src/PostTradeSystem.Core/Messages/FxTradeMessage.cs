namespace PostTradeSystem.Core.Messages;

public class FxTradeMessage : TradeMessage
{
    public string BaseCurrency { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = string.Empty;
    public DateTime SettlementDate { get; set; }
    public decimal SpotRate { get; set; }
    public decimal ForwardPoints { get; set; }
    public string TradeType { get; set; } = string.Empty;
    public string DeliveryMethod { get; set; } = string.Empty;

    public FxTradeMessage()
    {
        MessageType = "FX";
    }
}