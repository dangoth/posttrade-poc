namespace PostTradeSystem.Core.Services;

public class MockExternalDataService : IExternalDataService
{
    private static readonly string[] RiskProfiles = { "LOW", "MEDIUM", "HIGH", "STANDARD" };
    private static readonly string[] AccountTypes = { "INSTITUTIONAL", "RETAIL", "PROFESSIONAL", "ELIGIBLE_COUNTERPARTY" };
    private static readonly Random Random = new();

    public Task<string> GetRiskAssessmentScoreAsync(string traderId, string instrumentId, decimal notionalValue)
    {
        // Mock risk assessment based on notional value and trader characteristics
        var riskProfile = notionalValue switch
        {
            > 10_000_000 => GetRandomFromPool(new[] { "HIGH", "MEDIUM" }),
            > 1_000_000 => GetRandomFromPool(new[] { "MEDIUM", "STANDARD" }),
            > 100_000 => GetRandomFromPool(new[] { "STANDARD", "LOW" }),
            _ => GetRandomFromPool(new[] { "LOW", "STANDARD" })
        };

        return Task.FromResult(riskProfile);
    }

    public Task<string> GetAccountHolderDetailsAsync(string traderId)
    {
        // Mock account holder classification
        var accountType = GetRandomFromPool(AccountTypes);
        return Task.FromResult(accountType);
    }

    public Task<bool> ValidateRegulatoryComplianceAsync(string tradeType, string counterpartyId, decimal notionalValue)
    {
        // Mock regulatory compliance check - mostly pass with occasional failures
        var isCompliant = Random.NextDouble() > 0.05; // 95% compliance rate
        return Task.FromResult(isCompliant);
    }

    public Task<decimal> GetMarketDataEnrichmentAsync(string instrumentId, DateTime tradeDateTime)
    {
        // Mock market data enrichment - return a volatility factor
        var volatilityFactor = (decimal)(Random.NextDouble() * 0.5 + 0.1); // 0.1 to 0.6
        return Task.FromResult(volatilityFactor);
    }

    private static string GetRandomFromPool(string[] pool)
    {
        return pool[Random.Next(pool.Length)];
    }
}