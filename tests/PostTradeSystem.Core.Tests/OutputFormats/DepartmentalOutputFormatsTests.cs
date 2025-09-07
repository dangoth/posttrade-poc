using PostTradeSystem.Core.OutputFormats;
using Xunit;

namespace PostTradeSystem.Core.Tests.OutputFormats;

public class DepartmentalOutputFormatsTests
{
    [Fact]
    public void ComplianceOutput_ShouldInitializeWithCorrectDepartment()
    {
        var output = new ComplianceOutput();

        Assert.Equal("COMPLIANCE", output.Department);
        Assert.NotEmpty(output.OutputId);
        Assert.True(output.GeneratedAt <= DateTime.UtcNow);
        Assert.Empty(output.Trades);
        Assert.Empty(output.Alerts);
        Assert.NotNull(output.Summary);
    }

    [Fact]
    public void ComplianceOutput_ShouldAcceptTradeRecords()
    {
        var output = new ComplianceOutput();
        var tradeRecord = new ComplianceTradeRecord
        {
            TradeId = "TRADE-001",
            TraderId = "TRADER-001",
            InstrumentId = "AAPL",
            NotionalValue = 15050m,
            RegulatoryClassification = "MiFID_II_EQUITY",
            RiskProfile = "STANDARD",
            TradeDateTime = DateTime.UtcNow,
            Status = "EXECUTED"
        };

        output.Trades.Add(tradeRecord);

        Assert.Single(output.Trades);
        Assert.Equal("TRADE-001", output.Trades[0].TradeId);
        Assert.Equal("MiFID_II_EQUITY", output.Trades[0].RegulatoryClassification);
    }

    [Fact]
    public void ComplianceAlert_ShouldGenerateUniqueAlertId()
    {
        var alert1 = new ComplianceAlert();
        var alert2 = new ComplianceAlert();

        Assert.NotEqual(alert1.AlertId, alert2.AlertId);
        Assert.True(alert1.DetectedAt <= DateTime.UtcNow);
        Assert.True(alert2.DetectedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void RiskManagementOutput_ShouldInitializeWithCorrectDepartment()
    {
        var output = new RiskManagementOutput();

        Assert.Equal("RISK_MANAGEMENT", output.Department);
        Assert.NotEmpty(output.OutputId);
        Assert.Empty(output.Trades);
        Assert.Empty(output.Exposures);
        Assert.NotNull(output.Metrics);
    }

    [Fact]
    public void RiskManagementOutput_ShouldAcceptRiskTradeRecords()
    {
        var output = new RiskManagementOutput();
        var riskRecord = new RiskTradeRecord
        {
            TradeId = "TRADE-001",
            TraderId = "TRADER-001",
            InstrumentId = "AAPL",
            Quantity = 100m,
            Price = 150.50m,
            NotionalValue = 15050m,
            Direction = "BUY",
            Currency = "USD",
            TradeType = "EQUITY",
            RiskWeight = 0.15m,
            VaR = 750.25m
        };

        output.Trades.Add(riskRecord);

        Assert.Single(output.Trades);
        Assert.Equal("TRADE-001", output.Trades[0].TradeId);
        Assert.Equal(0.15m, output.Trades[0].RiskWeight);
        Assert.Equal(750.25m, output.Trades[0].VaR);
    }

    [Fact]
    public void ReportingOutput_ShouldInitializeWithCorrectDepartment()
    {
        var output = new ReportingOutput();

        Assert.Equal("REPORTING", output.Department);
        Assert.NotEmpty(output.OutputId);
        Assert.Empty(output.Trades);
        Assert.Empty(output.Breakdowns);
        Assert.NotNull(output.Summary);
    }

    [Fact]
    public void ReportingOutput_ShouldAcceptReportingTradeRecords()
    {
        var output = new ReportingOutput();
        var reportingRecord = new ReportingTradeRecord
        {
            TradeId = "TRADE-001",
            TraderId = "TRADER-001",
            InstrumentId = "AAPL",
            Quantity = 100m,
            Price = 150.50m,
            NotionalValue = 15050m,
            Direction = "BUY",
            TradeDateTime = DateTime.UtcNow,
            SettlementDate = DateTime.UtcNow.AddDays(2),
            Currency = "USD",
            TradeType = "EQUITY",
            Status = "EXECUTED",
            PnL = 250.75m
        };

        output.Trades.Add(reportingRecord);

        Assert.Single(output.Trades);
        Assert.Equal("TRADE-001", output.Trades[0].TradeId);
        Assert.Equal(250.75m, output.Trades[0].PnL);
    }

    [Fact]
    public void DepartmentalOutputBase_ShouldGenerateUniqueOutputIds()
    {
        var output1 = new ComplianceOutput();
        var output2 = new RiskManagementOutput();
        var output3 = new ReportingOutput();

        Assert.NotEqual(output1.OutputId, output2.OutputId);
        Assert.NotEqual(output2.OutputId, output3.OutputId);
        Assert.NotEqual(output1.OutputId, output3.OutputId);
    }
}