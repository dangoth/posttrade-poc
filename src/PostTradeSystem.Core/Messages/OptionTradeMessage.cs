namespace PostTradeSystem.Core.Messages;

public class OptionTradeMessage : TradeMessage
{
    public string UnderlyingSymbol { get; set; } = string.Empty;
    public decimal StrikePrice { get; set; }
    public DateTime ExpirationDate { get; set; }
    public string OptionType { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public decimal ImpliedVolatility { get; set; }
    public string ContractSize { get; set; } = string.Empty;
    public string SettlementType { get; set; } = string.Empty;

    public OptionTradeMessage()
    {
        MessageType = "OPTION";
    }
}