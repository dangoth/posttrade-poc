namespace PostTradeSystem.Core.Services;

public interface ITradeRiskService
{
    string ExtractRiskProfile(Dictionary<string, object> additionalData);
    string DetermineRegulatoryClassification(string tradeType);
    decimal CalculateNotionalValue(decimal quantity, decimal price);
}

public class TradeRiskService : ITradeRiskService
{
    public string ExtractRiskProfile(Dictionary<string, object> additionalData)
    {
        if (additionalData.TryGetValue("RiskProfile", out var riskProfile) && riskProfile != null)
        {
            return riskProfile.ToString() ?? "STANDARD";
        }
        return "STANDARD";
    }

    public string DetermineRegulatoryClassification(string tradeType)
    {
        return tradeType.ToUpper() switch
        {
            "EQUITY" => "MiFID_II_EQUITY",
            "OPTION" => "MiFID_II_DERIVATIVE",
            "FX" => "EMIR_FX",
            _ => "UNCLASSIFIED"
        };
    }

    public decimal CalculateNotionalValue(decimal quantity, decimal price)
    {
        return quantity * price;
    }
}