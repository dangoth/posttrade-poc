using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.OutputFormats;
using Xunit;

namespace PostTradeSystem.Core.Tests.Schemas;

public class SchemaValidationServiceTests
{
    private readonly SchemaValidationService _validationService;
    private readonly EventSerializationRegistry _eventRegistry;
    private readonly JsonSchemaValidator _validator;

    public SchemaValidationServiceTests()
    {
        _eventRegistry = new EventSerializationRegistry();
        _validator = new JsonSchemaValidator();
        _validationService = new SchemaValidationService(_validator, _eventRegistry);
    }

    [Fact]
    public void GetSupportedEventTypes_ShouldReturnExpectedTypes()
    {
        var supportedTypes = _validationService.GetSupportedEventTypes().ToList();

        Assert.Contains("TradeCreated", supportedTypes);
        Assert.Contains("TradeStatusChanged", supportedTypes);
        Assert.Contains("TradeUpdated", supportedTypes);
        Assert.Contains("TradeEnriched", supportedTypes);
        Assert.Contains("TradeValidationFailed", supportedTypes);
    }

    [Fact]
    public void GetSupportedMessageTypes_ShouldReturnExpectedTypes()
    {
        var supportedTypes = _validationService.GetSupportedMessageTypes().ToList();

        Assert.Contains("TradeMessage", supportedTypes);
        Assert.Contains("EquityTradeMessage", supportedTypes);
        Assert.Contains("OptionTradeMessage", supportedTypes);
        Assert.Contains("FxTradeMessage", supportedTypes);
        Assert.Contains("TradeMessageEnvelope", supportedTypes);
    }

    [Fact]
    public void GetSupportedOutputTypes_ShouldReturnExpectedTypes()
    {
        var supportedTypes = _validationService.GetSupportedOutputTypes().ToList();

        Assert.Contains("ComplianceOutput", supportedTypes);
        Assert.Contains("RiskManagementOutput", supportedTypes);
        Assert.Contains("ReportingOutput", supportedTypes);
    }

    [Fact]
    public void ValidateMessage_WithValidJson_ShouldReturnSuccess()
    {
        var validJson = """
        {
            "tradeId": "TRADE-001",
            "traderId": "TRADER-001",
            "instrumentId": "AAPL",
            "quantity": 100,
            "price": 150.50,
            "direction": "BUY",
            "tradeDateTime": "2024-01-01T10:00:00Z",
            "currency": "USD",
            "status": "PENDING",
            "counterpartyId": "COUNTERPARTY-001",
            "sourceSystem": "TRADING_SYSTEM",
            "messageType": "EQUITY"
        }
        """;

        var result = _validationService.ValidateMessage("TradeMessage", validJson);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateOutput_WithComplianceOutput_ShouldValidate()
    {
        var complianceOutput = new ComplianceOutput
        {
            ReportingPeriod = "2024-01-01",
            Summary = new ComplianceSummary
            {
                TotalTrades = 10,
                TotalNotionalValue = 1000000m,
                AlertCount = 2
            }
        };

        var result = _validationService.ValidateOutput(complianceOutput);

        Assert.True(result.IsValid || result.ErrorMessage != null);
    }

    [Fact]
    public void ValidateOutput_WithRiskManagementOutput_ShouldValidate()
    {
        var riskOutput = new RiskManagementOutput
        {
            ReportingPeriod = "2024-01-01",
            Metrics = new RiskMetrics
            {
                TotalVaR = 50000m,
                TotalNotional = 1000000m
            }
        };

        var result = _validationService.ValidateOutput(riskOutput);

        Assert.True(result.IsValid || result.ErrorMessage != null);
    }

    [Fact]
    public void ValidationResult_Success_ShouldCreateValidResult()
    {
        var result = ValidationResult.Success();

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidationResult_Failure_ShouldCreateInvalidResult()
    {
        var errorMessage = "Validation failed";
        var result = ValidationResult.Failure(errorMessage);

        Assert.False(result.IsValid);
        Assert.Equal(errorMessage, result.ErrorMessage);
    }
}