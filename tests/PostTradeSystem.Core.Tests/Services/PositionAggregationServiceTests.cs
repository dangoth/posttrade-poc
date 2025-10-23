using Microsoft.Extensions.Logging;
using Moq;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Core.Common;
using PostTradeSystem.Core.Schemas;
using Xunit;

namespace PostTradeSystem.Core.Tests.Services;

public class PositionAggregationServiceTests
{
    private readonly Mock<IJsonSchemaValidator> _mockSchemaValidator;
    private readonly Mock<ILogger<PositionAggregationService>> _mockLogger;
    private readonly PositionAggregationService _service;

    public PositionAggregationServiceTests()
    {
        _mockSchemaValidator = new Mock<IJsonSchemaValidator>();
        _mockLogger = new Mock<ILogger<PositionAggregationService>>();
        
        _mockSchemaValidator.Setup(x => x.ValidateMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>())).Returns(true);
        
        _service = new PositionAggregationService(_mockSchemaValidator.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CalculatePositionFromEventsAsync_WithBuyTrades_ReturnsLongPosition()
    {
        // Arrange
        var traderId = "TRADER001";
        var instrumentId = "AAPL";
        var events = new List<TradeCreatedEvent>
        {
            new TradeCreatedEvent(
                "TRADE001", traderId, instrumentId, 100, 150.00m, "Buy", 
                DateTime.UtcNow.AddHours(-2), "USD", "COUNTERPARTY001", "EQUITY", 
                1, "CORR001", "SYSTEM", new Dictionary<string, object>()),
            new TradeCreatedEvent(
                "TRADE002", traderId, instrumentId, 50, 155.00m, "Buy", 
                DateTime.UtcNow.AddHours(-1), "USD", "COUNTERPARTY001", "EQUITY", 
                2, "CORR002", "SYSTEM", new Dictionary<string, object>())
        };

        // Act
        var result = await _service.CalculatePositionFromEventsAsync(events, traderId, instrumentId);

        // Assert
        Assert.True(result.IsSuccess);
        var position = result.Value!;
        Assert.Equal(150m, position.NetQuantity);
        Assert.Equal(151.67m, Math.Round(position.AveragePrice, 2));
        Assert.Equal(22750m, position.TotalNotional);
        Assert.Equal("USD", position.Currency);
        Assert.Equal(2, position.TradeCount);
        Assert.True((bool)position.AdditionalMetrics["IsLongPosition"]);
        Assert.False((bool)position.AdditionalMetrics["IsShortPosition"]);
    }

    [Fact]
    public async Task CalculatePositionFromEventsAsync_WithBuyAndSellTrades_ReturnsNetPosition()
    {
        // Arrange
        var traderId = "TRADER001";
        var instrumentId = "AAPL";
        var events = new List<TradeCreatedEvent>
        {
            new TradeCreatedEvent(
                "TRADE001", traderId, instrumentId, 100, 150.00m, "Buy", 
                DateTime.UtcNow.AddHours(-2), "USD", "COUNTERPARTY001", "EQUITY", 
                1, "CORR001", "SYSTEM", new Dictionary<string, object>()),
            new TradeCreatedEvent(
                "TRADE002", traderId, instrumentId, 30, 160.00m, "Sell", 
                DateTime.UtcNow.AddHours(-1), "USD", "COUNTERPARTY001", "EQUITY", 
                2, "CORR002", "SYSTEM", new Dictionary<string, object>())
        };

        // Act
        var result = await _service.CalculatePositionFromEventsAsync(events, traderId, instrumentId);

        // Assert
        Assert.True(result.IsSuccess);
        var position = result.Value!;
        Assert.Equal(70m, position.NetQuantity);
        Assert.Equal(10200m, position.TotalNotional);
        Assert.Equal(2, position.TradeCount);
        Assert.True(position.RealizedPnL > 0); // Sold at higher price
    }

    [Fact]
    public async Task CalculatePositionFromEventsAsync_WithFlatPosition_ReturnsZeroPosition()
    {
        // Arrange
        var traderId = "TRADER001";
        var instrumentId = "AAPL";
        var events = new List<TradeCreatedEvent>
        {
            new TradeCreatedEvent(
                "TRADE001", traderId, instrumentId, 100, 150.00m, "Buy", 
                DateTime.UtcNow.AddHours(-2), "USD", "COUNTERPARTY001", "EQUITY", 
                1, "CORR001", "SYSTEM", new Dictionary<string, object>()),
            new TradeCreatedEvent(
                "TRADE002", traderId, instrumentId, 100, 160.00m, "Sell", 
                DateTime.UtcNow.AddHours(-1), "USD", "COUNTERPARTY001", "EQUITY", 
                2, "CORR002", "SYSTEM", new Dictionary<string, object>())
        };

        // Act
        var result = await _service.CalculatePositionFromEventsAsync(events, traderId, instrumentId);

        // Assert
        Assert.True(result.IsSuccess);
        var position = result.Value!;
        Assert.Equal(0m, position.NetQuantity);
        Assert.Equal(0m, position.TotalNotional);
        Assert.Equal(1000m, position.RealizedPnL); // 100 * (160 - 150)
        Assert.True((bool)position.AdditionalMetrics["IsFlatPosition"]);
    }

    [Fact]
    public async Task CalculatePositionFromEventsAsync_WithValidationFailure_ReturnsFailure()
    {
        // Arrange
        var traderId = "TRADER001";
        var instrumentId = "AAPL";
        var events = new List<TradeCreatedEvent>
        {
            new TradeCreatedEvent(
                "TRADE001", traderId, instrumentId, 100, 150.00m, "Buy", 
                DateTime.UtcNow, "USD", "COUNTERPARTY001", "EQUITY", 
                1, "CORR001", "SYSTEM", new Dictionary<string, object>())
        };

        // Mock validator to return false to test business logic handling of validation failure
        _mockSchemaValidator.Setup(x => x.ValidateMessage("PositionSummary", It.IsAny<string>(), null)).Returns(false);

        // Act
        var result = await _service.CalculatePositionFromEventsAsync(events, traderId, instrumentId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Position summary validation failed", result.Error);
        
        // Verify that validation was called with correct parameters
        _mockSchemaValidator.Verify(x => x.ValidateMessage("PositionSummary", It.IsAny<string>(), null), Times.Once);
    }
}