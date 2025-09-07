namespace PostTradeSystem.Core.OutputFormats;

public abstract class DepartmentalOutputBase
{
    public string OutputId { get; set; } = Guid.NewGuid().ToString();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string Department { get; set; } = string.Empty;
    public string ReportingPeriod { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class ComplianceOutput : DepartmentalOutputBase
{
    public ComplianceOutput()
    {
        Department = "COMPLIANCE";
    }

    public List<ComplianceTradeRecord> Trades { get; set; } = new();
    public ComplianceSummary Summary { get; set; } = new();
    public List<ComplianceAlert> Alerts { get; set; } = new();
}

public class ComplianceTradeRecord
{
    public string TradeId { get; set; } = string.Empty;
    public string TraderId { get; set; } = string.Empty;
    public string InstrumentId { get; set; } = string.Empty;
    public decimal NotionalValue { get; set; }
    public string RegulatoryClassification { get; set; } = string.Empty;
    public string RiskProfile { get; set; } = string.Empty;
    public DateTime TradeDateTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> ComplianceFlags { get; set; } = new();
    public Dictionary<string, object> RegulatoryData { get; set; } = new();
}

public class ComplianceSummary
{
    public int TotalTrades { get; set; }
    public decimal TotalNotionalValue { get; set; }
    public Dictionary<string, int> TradesByClassification { get; set; } = new();
    public Dictionary<string, int> TradesByRiskProfile { get; set; } = new();
    public int AlertCount { get; set; }
}

public class ComplianceAlert
{
    public string AlertId { get; set; } = Guid.NewGuid().ToString();
    public string TradeId { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> AlertData { get; set; } = new();
}

public class RiskManagementOutput : DepartmentalOutputBase
{
    public RiskManagementOutput()
    {
        Department = "RISK_MANAGEMENT";
    }

    public List<RiskTradeRecord> Trades { get; set; } = new();
    public RiskMetrics Metrics { get; set; } = new();
    public List<RiskExposure> Exposures { get; set; } = new();
}

public class RiskTradeRecord
{
    public string TradeId { get; set; } = string.Empty;
    public string TraderId { get; set; } = string.Empty;
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal NotionalValue { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string TradeType { get; set; } = string.Empty;
    public decimal RiskWeight { get; set; }
    public decimal VaR { get; set; }
    public Dictionary<string, decimal> GreekValues { get; set; } = new();
}

public class RiskMetrics
{
    public decimal TotalVaR { get; set; }
    public decimal TotalNotional { get; set; }
    public Dictionary<string, decimal> VaRByAssetClass { get; set; } = new();
    public Dictionary<string, decimal> ExposureByCounterparty { get; set; } = new();
    public Dictionary<string, decimal> ExposureByCurrency { get; set; } = new();
}

public class RiskExposure
{
    public string ExposureType { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public decimal GrossExposure { get; set; }
    public decimal NetExposure { get; set; }
    public decimal Limit { get; set; }
    public decimal UtilizationPercentage { get; set; }
}

public class ReportingOutput : DepartmentalOutputBase
{
    public ReportingOutput()
    {
        Department = "REPORTING";
    }

    public List<ReportingTradeRecord> Trades { get; set; } = new();
    public ReportingSummary Summary { get; set; } = new();
    public List<ReportingBreakdown> Breakdowns { get; set; } = new();
}

public class ReportingTradeRecord
{
    public string TradeId { get; set; } = string.Empty;
    public string TraderId { get; set; } = string.Empty;
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal NotionalValue { get; set; }
    public string Direction { get; set; } = string.Empty;
    public DateTime TradeDateTime { get; set; }
    public DateTime SettlementDate { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string TradeType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal PnL { get; set; }
    public Dictionary<string, object> ReportingAttributes { get; set; } = new();
}

public class ReportingSummary
{
    public int TotalTrades { get; set; }
    public decimal TotalNotionalValue { get; set; }
    public decimal TotalPnL { get; set; }
    public Dictionary<string, int> TradesByType { get; set; } = new();
    public Dictionary<string, decimal> VolumeByAssetClass { get; set; } = new();
    public Dictionary<string, decimal> PnLByTrader { get; set; } = new();
}

public class ReportingBreakdown
{
    public string BreakdownType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int TradeCount { get; set; }
    public decimal TotalVolume { get; set; }
    public decimal TotalPnL { get; set; }
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}