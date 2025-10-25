using PostTradeSystem.Core.Services;
using Xunit;
using System.Diagnostics;

namespace PostTradeSystem.Core.Tests.Services;

public class ConfigurableExternalDataServiceTests
{
    private readonly MockExternalDataService _mockService = new();
    private readonly ConfigurableExternalDataService _configurableService;

    public ConfigurableExternalDataServiceTests()
    {
        _configurableService = new ConfigurableExternalDataService(_mockService);
    }

    [Fact]
    public async Task GetRiskAssessmentScoreAsync_WithDefaultConfig_ReturnsValidResult()
    {
        // Arrange
        var traderId = "TRADER001";
        var instrumentId = "AAPL";
        var notionalValue = 1_000_000m;

        // Act
        var result = await _configurableService.GetRiskAssessmentScoreAsync(traderId, instrumentId, notionalValue);

        // Assert
        Assert.Contains(result, new[] { "LOW", "MEDIUM", "HIGH", "STANDARD" });
    }

    [Fact]
    public async Task GetRiskAssessmentScoreAsync_WithConfiguredLatency_TakesExpectedTime()
    {
        // Arrange
        var minLatency = TimeSpan.FromMilliseconds(100);
        var maxLatency = TimeSpan.FromMilliseconds(200);
        _configurableService.ConfigureLatency(minLatency, maxLatency);

        var stopwatch = Stopwatch.StartNew();

        // Act
        await _configurableService.GetRiskAssessmentScoreAsync("TRADER001", "AAPL", 1_000_000m);

        // Assert
        stopwatch.Stop();
        Assert.True(stopwatch.ElapsedMilliseconds >= minLatency.TotalMilliseconds - 10); // Allow 10ms tolerance
    }

    [Fact]
    public async Task GetRiskAssessmentScoreAsync_WithHighFailureRate_ReturnsFallbackValue()
    {
        // Arrange
        _configurableService.ConfigureFailureRate(1.0); // 100% failure rate
        _configurableService.ConfigureLatency(TimeSpan.Zero, TimeSpan.Zero); // No latency for faster test

        // Act
        var result = await _configurableService.GetRiskAssessmentScoreAsync("TRADER001", "AAPL", 1_000_000m);

        // Assert
        Assert.Equal("STANDARD", result); // Should return fallback value
    }

    [Fact]
    public async Task GetRiskAssessmentScoreAsync_WithCircuitBreaker_ReturnsFallbackAfterFailures()
    {
        // Arrange
        _configurableService.ConfigureFailureRate(1.0); // 100% failure rate
        _configurableService.ConfigureCircuitBreaker(2, TimeSpan.FromMinutes(1));
        _configurableService.ConfigureLatency(TimeSpan.Zero, TimeSpan.Zero); // No latency for faster test

        // Act - Trigger circuit breaker by calling multiple times
        var result1 = await _configurableService.GetRiskAssessmentScoreAsync("TRADER001", "AAPL", 1_000_000m);
        var result2 = await _configurableService.GetRiskAssessmentScoreAsync("TRADER001", "AAPL", 1_000_000m);
        var result3 = await _configurableService.GetRiskAssessmentScoreAsync("TRADER001", "AAPL", 1_000_000m);

        // Assert - All should return fallback values
        Assert.Equal("STANDARD", result1);
        Assert.Equal("STANDARD", result2);
        Assert.Equal("STANDARD", result3);
    }

    [Fact]
    public void ConfigureLatency_WithValidValues_SetsConfiguration()
    {
        // Arrange
        var minLatency = TimeSpan.FromMilliseconds(50);
        var maxLatency = TimeSpan.FromMilliseconds(150);

        // Act & Assert - Should not throw
        _configurableService.ConfigureLatency(minLatency, maxLatency);
    }

    [Fact]
    public void ConfigureFailureRate_WithValidValues_SetsConfiguration()
    {
        // Act & Assert - Should not throw
        _configurableService.ConfigureFailureRate(0.05); // 5%
        _configurableService.ConfigureFailureRate(0.0);  // 0%
        _configurableService.ConfigureFailureRate(1.0);  // 100%
    }

    [Fact]
    public void ConfigureCircuitBreaker_WithValidValues_SetsConfiguration()
    {
        // Act & Assert - Should not throw
        _configurableService.ConfigureCircuitBreaker(5, TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task AllMethods_WithZeroFailureRate_NeverThrow()
    {
        // Arrange
        _configurableService.ConfigureFailureRate(0.0);
        _configurableService.ConfigureLatency(TimeSpan.Zero, TimeSpan.Zero);

        // Act & Assert - All should succeed
        var riskScore = await _configurableService.GetRiskAssessmentScoreAsync("TRADER001", "AAPL", 1_000_000m);
        var accountDetails = await _configurableService.GetAccountHolderDetailsAsync("TRADER001");
        var compliance = await _configurableService.ValidateRegulatoryComplianceAsync("EQUITY", "COUNTERPARTY001", 1_000_000m);
        var marketData = await _configurableService.GetMarketDataEnrichmentAsync("AAPL", DateTime.UtcNow);

        Assert.NotNull(riskScore);
        Assert.NotNull(accountDetails);
        Assert.IsType<bool>(compliance);
        Assert.True(marketData > 0);
    }
}