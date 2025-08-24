namespace PostTradeSystem.Core.Messages;

public class EquityTradeMessage : TradeMessage
{
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public decimal DividendRate { get; set; }
    public string Isin { get; set; } = string.Empty;
    public string MarketSegment { get; set; } = string.Empty;

    public EquityTradeMessage()
    {
        MessageType = "EQUITY";
    }
}