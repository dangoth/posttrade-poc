namespace PostTradeSystem.Core.Models;

public class EquityTrade : TradeBase
{
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public decimal DividendRate { get; set; }
}