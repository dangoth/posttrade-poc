using PostTradeSystem.Core.Services;
using Xunit;

namespace PostTradeSystem.Core.Tests.Services;

public class TradeRiskServiceTests
{
    private readonly TradeRiskService _service;

    public TradeRiskServiceTests()
    {
        _service = new TradeRiskService();
    }

    [Fact]
    public void ExtractRiskProfile_WithRiskProfileInData_ReturnsRiskProfile()
    {
        var additionalData = new Dictionary<string, object>
        {
            ["RiskProfile"] = "HIGH"
        };

        var result = _service.ExtractRiskProfile(additionalData);

        Assert.Equal("HIGH", result);
    }

    [Fact]
    public void ExtractRiskProfile_WithoutRiskProfileInData_ReturnsStandard()
    {
        var additionalData = new Dictionary<string, object>();

        var result = _service.ExtractRiskProfile(additionalData);

        Assert.Equal("STANDARD", result);
    }

    [Fact]
    public void ExtractRiskProfile_WithNullRiskProfile_ReturnsStandard()
    {
        var additionalData = new Dictionary<string, object>
        {
            ["RiskProfile"] = null!
        };

        var result = _service.ExtractRiskProfile(additionalData);

        Assert.Equal("STANDARD", result);
    }

    [Theory]
    [InlineData("EQUITY", "MiFID_II_EQUITY")]
    [InlineData("equity", "MiFID_II_EQUITY")]
    [InlineData("OPTION", "MiFID_II_DERIVATIVE")]
    [InlineData("option", "MiFID_II_DERIVATIVE")]
    [InlineData("FX", "EMIR_FX")]
    [InlineData("fx", "EMIR_FX")]
    [InlineData("UNKNOWN", "UNCLASSIFIED")]
    [InlineData("", "UNCLASSIFIED")]
    public void DetermineRegulatoryClassification_ReturnsCorrectClassification(string tradeType, string expected)
    {
        var result = _service.DetermineRegulatoryClassification(tradeType);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(100, 50.5, 5050)]
    [InlineData(0, 100, 0)]
    [InlineData(10, 0, 0)]
    [InlineData(1.5, 2.5, 3.75)]
    public void CalculateNotionalValue_ReturnsCorrectValue(decimal quantity, decimal price, decimal expected)
    {
        var result = _service.CalculateNotionalValue(quantity, price);

        Assert.Equal(expected, result);
    }
}