namespace PostTradeSystem.Core.Models;

public class FxTrade : TradeBase
{
    public string BaseCurrency { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = string.Empty;
    public DateTime SettlementDate { get; set; }
    public decimal SpotRate { get; set; }
    public decimal ForwardPoints { get; set; }
}