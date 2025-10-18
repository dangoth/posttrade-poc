using Microsoft.Extensions.Logging;
using Moq;
using PostTradeSystem.Core.Adapters;
using PostTradeSystem.Core.Messages;
using System.Text.Json;
using Xunit;

namespace PostTradeSystem.Core.Tests.Adapters;

public class TradeMessageAdapterFactoryTests
{
    private readonly Mock<ILogger<TradeMessageAdapterFactory>> _mockLogger;
    private readonly TradeMessageAdapterFactory _factory;

    public TradeMessageAdapterFactoryTests()
    {
        _mockLogger = new Mock<ILogger<TradeMessageAdapterFactory>>();
        _factory = new TradeMessageAdapterFactory(_mockLogger.Object);
    }

    [Fact]
    public async Task ProcessMessageAsync_WithValidEquityMessage_ReturnsTradeCreatedEvent()
    {
        // Arrange
        var equityMessage = new EquityTradeMessage
        {
            TradeId = "TRADE001",
            TraderId = "TRADER001",
            InstrumentId = "AAPL",
            Quantity = 100,
            Price = 150.50m,
            Direction = "BUY",
            TradeDateTime = DateTime.UtcNow,
            Currency = "USD",
            CounterpartyId = "CP001",
            Symbol = "AAPL",
            Exchange = "NASDAQ",
            Sector = "Technology"
        };

        var envelope = new TradeMessageEnvelope<EquityTradeMessage>
        {
            MessageId = "MSG001",
            Timestamp = DateTime.UtcNow,
            Version = "1.0",
            CorrelationId = "CORR001",
            Payload = equityMessage
        };

        var messageValue = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var result = await _factory.ProcessMessageAsync("EQUITY", "EQUITY_SYSTEM", messageValue, "CORR001");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TRADE001", result.AggregateId);
        Assert.Equal("TRADER001", result.TraderId);
        Assert.Equal("AAPL", result.InstrumentId);
        Assert.Equal("EQUITY", result.TradeType);
        Assert.Equal("CORR001", result.CorrelationId);
    }

    [Fact]
    public async Task ProcessMessageAsync_WithValidFxMessage_ReturnsTradeCreatedEvent()
    {
        // Arrange
        var fxMessage = new FxTradeMessage
        {
            TradeId = "FX001",
            TraderId = "TRADER002",
            InstrumentId = "EURUSD",
            Quantity = 1000000,
            Price = 1.0850m,
            Direction = "BUY",
            TradeDateTime = DateTime.UtcNow,
            Currency = "USD",
            CounterpartyId = "CP002",
            BaseCurrency = "EUR",
            QuoteCurrency = "USD",
            SpotRate = 1.0850m
        };

        var envelope = new TradeMessageEnvelope<FxTradeMessage>
        {
            MessageId = "MSG002",
            Timestamp = DateTime.UtcNow,
            Version = "1.0",
            CorrelationId = "CORR002",
            Payload = fxMessage
        };

        var messageValue = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var result = await _factory.ProcessMessageAsync("FX", "FX_SYSTEM", messageValue, "CORR002");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("FX001", result.AggregateId);
        Assert.Equal("TRADER002", result.TraderId);
        Assert.Equal("EURUSD", result.InstrumentId);
        Assert.Equal("FX", result.TradeType);
        Assert.Equal("CORR002", result.CorrelationId);
    }

    [Fact]
    public async Task ProcessMessageAsync_WithInvalidMessageType_ReturnsNull()
    {
        // Arrange
        var messageValue = "{}";

        // Act
        var result = await _factory.ProcessMessageAsync("INVALID", "UNKNOWN_SYSTEM", messageValue, "CORR003");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessMessageAsync_WithInvalidJson_ReturnsNull()
    {
        // Arrange
        var invalidJson = "invalid json";

        // Act
        var result = await _factory.ProcessMessageAsync("EQUITY", "EQUITY_SYSTEM", invalidJson, "CORR004");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("EQUITY", "EQUITY_SYSTEM", true)]
    [InlineData("FX", "FX_SYSTEM", true)]
    [InlineData("OPTION", "OPTION_SYSTEM", true)]
    [InlineData("INVALID", "UNKNOWN_SYSTEM", false)]
    [InlineData("EQUITY", "", true)]
    public void CanProcessMessage_WithVariousInputs_ReturnsExpectedResult(string messageType, string sourceSystem, bool expected)
    {
        // Act
        var result = _factory.CanProcessMessage(messageType, sourceSystem);

        // Assert
        Assert.Equal(expected, result);
    }
}