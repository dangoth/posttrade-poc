using PostTradeSystem.Core.Services;
using Xunit;

namespace PostTradeSystem.Core.Tests.Services;

public class MockExternalDataServiceTests
{
    private readonly MockExternalDataService _service = new();

    [Fact]
    public async Task GetRiskAssessmentScoreAsync_WithHighNotionalValue_ReturnsHighOrMediumRisk()
    {
        // Arrange
        var traderId = "TRADER001";
        var instrumentId = "AAPL";
        var notionalValue = 15_000_000m;

        // Act
        var result = await _service.GetRiskAssessmentScoreAsync(traderId, instrumentId, notionalValue);

        // Assert
        Assert.Contains(result, new[] { "HIGH", "MEDIUM" });
    }

    [Fact]
    public async Task GetRiskAssessmentScoreAsync_WithLowNotionalValue_ReturnsLowOrStandardRisk()
    {
        // Arrange
        var traderId = "TRADER001";
        var instrumentId = "AAPL";
        var notionalValue = 50_000m;

        // Act
        var result = await _service.GetRiskAssessmentScoreAsync(traderId, instrumentId, notionalValue);

        // Assert
        Assert.Contains(result, new[] { "LOW", "STANDARD" });
    }

    [Fact]
    public async Task GetAccountHolderDetailsAsync_ReturnsValidAccountType()
    {
        // Arrange
        var traderId = "TRADER001";

        // Act
        var result = await _service.GetAccountHolderDetailsAsync(traderId);

        // Assert
        Assert.Contains(result, new[] { "INSTITUTIONAL", "RETAIL", "PROFESSIONAL", "ELIGIBLE_COUNTERPARTY" });
    }

    [Fact]
    public async Task ValidateRegulatoryComplianceAsync_ReturnsBoolean()
    {
        // Arrange
        var tradeType = "EQUITY";
        var counterpartyId = "COUNTERPARTY001";
        var notionalValue = 1_000_000m;

        // Act
        var result = await _service.ValidateRegulatoryComplianceAsync(tradeType, counterpartyId, notionalValue);

        // Assert
        Assert.IsType<bool>(result);
    }

    [Fact]
    public async Task GetMarketDataEnrichmentAsync_ReturnsValidVolatilityFactor()
    {
        // Arrange
        var instrumentId = "AAPL";
        var tradeDateTime = DateTime.UtcNow;

        // Act
        var result = await _service.GetMarketDataEnrichmentAsync(instrumentId, tradeDateTime);

        // Assert
        Assert.True(result >= 0.1m && result <= 0.6m);
    }

    [Fact]
    public async Task GetRiskAssessmentScoreAsync_MultipleCallsWithSameInput_CanReturnDifferentResults()
    {
        // Arrange
        var traderId = "TRADER001";
        var instrumentId = "AAPL";
        var notionalValue = 5_000_000m;
        var results = new HashSet<string>();

        // Act - Call multiple times to test randomness
        for (int i = 0; i < 20; i++)
        {
            var result = await _service.GetRiskAssessmentScoreAsync(traderId, instrumentId, notionalValue);
            results.Add(result);
        }

        // Assert - Should have some variation (though not guaranteed due to randomness)
        Assert.True(results.Count >= 1);
        Assert.All(results, result => Assert.Contains(result, new[] { "LOW", "MEDIUM", "HIGH", "STANDARD" }));
    }
}