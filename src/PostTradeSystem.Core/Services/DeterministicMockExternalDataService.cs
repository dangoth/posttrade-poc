namespace PostTradeSystem.Core.Services;

public class DeterministicMockExternalDataService : IExternalDataService
{
    public Task<string> GetRiskAssessmentScoreAsync(string traderId, string instrumentId, decimal notionalValue)
    {
        var riskProfile = notionalValue switch
        {
            > 10_000_000 => "LOW",
            > 1_000_000 => "LOW", 
            > 100_000 => "LOW",
            _ => "LOW"
        };

        return Task.FromResult(riskProfile);
    }

    public Task<string> GetAccountHolderDetailsAsync(string traderId)
    {
        return Task.FromResult("RETAIL");
    }

    public Task<bool> ValidateRegulatoryComplianceAsync(string tradeType, string counterpartyId, decimal notionalValue)
    {
        return Task.FromResult(true);
    }

    public Task<decimal> GetMarketDataEnrichmentAsync(string instrumentId, DateTime tradeDateTime)
    {
        var hash = instrumentId.GetHashCode();
        var volatilityFactor = 0.1m + (Math.Abs(hash) % 50) / 100m;
        return Task.FromResult(volatilityFactor);
    }
}