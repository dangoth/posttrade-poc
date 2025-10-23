using Xunit;
using PostTradeSystem.Core.Schemas;

namespace PostTradeSystem.Core.Tests.Schemas;

public class PositionSummarySchemaValidationTests
{
    private readonly JsonSchemaValidator _validator;

    public PositionSummarySchemaValidationTests()
    {
        _validator = new JsonSchemaValidator();
        _validator.RegisterSchema("PositionSummary", MessageSchemas.PositionSummarySchema);
    }

    [Fact]
    public void PositionSummarySchema_ValidData_ShouldPass()
    {
        // Arrange
        var validJson = """
        {
            "traderId": "TRADER001",
            "instrumentId": "AAPL",
            "netQuantity": 100.0,
            "averagePrice": 150.25,
            "totalNotional": 15025.0,
            "currency": "USD",
            "lastUpdated": "2025-10-18T09:00:00.000Z",
            "tradeCount": 2,
            "realizedPnL": 500.0,
            "additionalMetrics": {
                "isLongPosition": true,
                "totalBuyQuantity": 100.0
            }
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("PositionSummary", validJson, null);

        // Assert
        Assert.True(isValid, "Valid PositionSummary should pass validation");
    }

    [Fact]
    public void PositionSummarySchema_MissingTraderId_ShouldFail()
    {
        // Arrange
        var invalidJson = """
        {
            "instrumentId": "AAPL",
            "netQuantity": 100.0,
            "averagePrice": 150.25,
            "totalNotional": 15025.0,
            "currency": "USD",
            "lastUpdated": "2025-10-18T09:00:00.000Z",
            "tradeCount": 2,
            "realizedPnL": 500.0,
            "additionalMetrics": {}
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("PositionSummary", invalidJson, null);

        // Assert
        Assert.False(isValid, "PositionSummary missing traderId should fail validation");
    }

    [Fact]
    public void PositionSummarySchema_MissingInstrumentId_ShouldFail()
    {
        // Arrange
        var invalidJson = """
        {
            "traderId": "TRADER001",
            "netQuantity": 100.0,
            "averagePrice": 150.25,
            "totalNotional": 15025.0,
            "currency": "USD",
            "lastUpdated": "2025-10-18T09:00:00.000Z",
            "tradeCount": 2,
            "realizedPnL": 500.0,
            "additionalMetrics": {}
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("PositionSummary", invalidJson, null);

        // Assert
        Assert.False(isValid, "PositionSummary missing instrumentId should fail validation");
    }

    [Fact]
    public void PositionSummarySchema_MissingMultipleRequiredFields_ShouldFail()
    {
        // Arrange
        var invalidJson = """
        {
            "traderId": "TRADER001",
            "netQuantity": 100.0,
            "averagePrice": 150.25
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("PositionSummary", invalidJson, null);

        // Assert
        Assert.False(isValid, "PositionSummary missing multiple required fields should fail validation");
    }

    [Fact]
    public void PositionSummarySchema_NetQuantityAsString_ShouldFail()
    {
        // Arrange
        var invalidJson = """
        {
            "traderId": "TRADER001",
            "instrumentId": "AAPL",
            "netQuantity": "not_a_number",
            "averagePrice": 150.25,
            "totalNotional": 15025.0,
            "currency": "USD",
            "lastUpdated": "2025-10-18T09:00:00.000Z",
            "tradeCount": 2,
            "realizedPnL": 500.0,
            "additionalMetrics": {}
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("PositionSummary", invalidJson, null);

        // Assert
        Assert.False(isValid, "PositionSummary with string netQuantity should fail validation");
    }

    [Fact]
    public void PositionSummarySchema_AveragePriceAsString_ShouldFail()
    {
        // Arrange
        var invalidJson = """
        {
            "traderId": "TRADER001",
            "instrumentId": "AAPL",
            "netQuantity": 100.0,
            "averagePrice": "invalid_price",
            "totalNotional": 15025.0,
            "currency": "USD",
            "lastUpdated": "2025-10-18T09:00:00.000Z",
            "tradeCount": 2,
            "realizedPnL": 500.0,
            "additionalMetrics": {}
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("PositionSummary", invalidJson, null);

        // Assert
        Assert.False(isValid, "PositionSummary with string averagePrice should fail validation");
    }

    [Fact]
    public void PositionSummarySchema_InvalidDateFormat_ShouldFail()
    {
        // Arrange - Invalid date format
        var invalidJson = """
        {
            "traderId": "TRADER001",
            "instrumentId": "AAPL",
            "netQuantity": 100.0,
            "averagePrice": 150.25,
            "totalNotional": 15025.0,
            "currency": "USD",
            "lastUpdated": "invalid-date-format",
            "tradeCount": 2,
            "realizedPnL": 500.0,
            "additionalMetrics": {}
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("PositionSummary", invalidJson, null);

        // Assert
        Assert.False(isValid, "PositionSummary with invalid date format should fail validation");
    }

    [Fact]
    public void PositionSummarySchema_TradeCountAsFloat_ShouldFail()
    {
        // Arrange - tradeCount should be integer, not float
        var invalidJson = """
        {
            "traderId": "TRADER001",
            "instrumentId": "AAPL",
            "netQuantity": 100.0,
            "averagePrice": 150.25,
            "totalNotional": 15025.0,
            "currency": "USD",
            "lastUpdated": "2025-10-18T09:00:00.000Z",
            "tradeCount": 2.5,
            "realizedPnL": 500.0,
            "additionalMetrics": {}
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("PositionSummary", invalidJson, null);

        // Assert
        Assert.False(isValid, "PositionSummary with float tradeCount should fail validation (should be integer)");
    }

    [Fact]
    public void PositionSummarySchema_TradeCountAsString_ShouldFail()
    {
        // Arrange - Testing if validator properly validates integer type
        var invalidJson = """
        {
            "traderId": "TRADER001",
            "instrumentId": "AAPL",
            "netQuantity": 100.0,
            "averagePrice": 150.25,
            "totalNotional": 15025.0,
            "currency": "USD",
            "lastUpdated": "2025-10-18T09:00:00.000Z",
            "tradeCount": "two",
            "realizedPnL": 500.0,
            "additionalMetrics": {}
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("PositionSummary", invalidJson, null);

        // Assert
        Assert.False(isValid, "PositionSummary with string tradeCount should fail validation");
    }

    [Fact]
    public void PositionSummarySchema_MissingAdditionalMetrics_ShouldFail()
    {
        // Arrange
        var invalidJson = """
        {
            "traderId": "TRADER001",
            "instrumentId": "AAPL",
            "netQuantity": 100.0,
            "averagePrice": 150.25,
            "totalNotional": 15025.0,
            "currency": "USD",
            "lastUpdated": "2025-10-18T09:00:00.000Z",
            "tradeCount": 2,
            "realizedPnL": 500.0
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("PositionSummary", invalidJson, null);

        // Assert
        Assert.False(isValid, "PositionSummary missing additionalMetrics should fail validation");
    }

    [Fact]
    public void PositionSummarySchema_AdditionalMetricsAsString_ShouldFail()
    {
        // Arrange
        var invalidJson = """
        {
            "traderId": "TRADER001",
            "instrumentId": "AAPL",
            "netQuantity": 100.0,
            "averagePrice": 150.25,
            "totalNotional": 15025.0,
            "currency": "USD",
            "lastUpdated": "2025-10-18T09:00:00.000Z",
            "tradeCount": 2,
            "realizedPnL": 500.0,
            "additionalMetrics": "not_an_object"
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("PositionSummary", invalidJson, null);

        // Assert
        Assert.False(isValid, "PositionSummary with string additionalMetrics should fail validation (should be object)");
    }

    [Fact]
    public void PositionSummarySchema_EmptyAdditionalMetrics_ShouldPass()
    {
        // Arrange
        var validJson = """
        {
            "traderId": "TRADER001",
            "instrumentId": "AAPL",
            "netQuantity": 100.0,
            "averagePrice": 150.25,
            "totalNotional": 15025.0,
            "currency": "USD",
            "lastUpdated": "2025-10-18T09:00:00.000Z",
            "tradeCount": 2,
            "realizedPnL": 500.0,
            "additionalMetrics": {}
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("PositionSummary", validJson, null);

        // Assert
        Assert.True(isValid, "PositionSummary with empty additionalMetrics object should pass validation");
    }
}