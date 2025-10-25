namespace PostTradeSystem.Core.Services;

public interface IExternalDataService
{
    Task<string> GetRiskAssessmentScoreAsync(string traderId, string instrumentId, decimal notionalValue);
    Task<string> GetAccountHolderDetailsAsync(string traderId);
    Task<bool> ValidateRegulatoryComplianceAsync(string tradeType, string counterpartyId, decimal notionalValue);
    Task<decimal> GetMarketDataEnrichmentAsync(string instrumentId, DateTime tradeDateTime);
}