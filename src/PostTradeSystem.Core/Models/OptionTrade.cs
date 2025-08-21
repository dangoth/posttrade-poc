namespace PostTradeSystem.Core.Models;

public class OptionTrade : TradeBase
{
    public string UnderlyingSymbol { get; set; } = string.Empty;
    public decimal StrikePrice { get; set; }
    public DateTime ExpirationDate { get; set; }
    public OptionType OptionType { get; set; }
    public string Exchange { get; set; } = string.Empty;
    public decimal ImpliedVolatility { get; set; }
}

public enum OptionType
{
    Call,
    Put
}