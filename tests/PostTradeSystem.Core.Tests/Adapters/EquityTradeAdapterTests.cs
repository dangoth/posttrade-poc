using PostTradeSystem.Core.Adapters;
using PostTradeSystem.Core.Messages;
using Xunit;

namespace PostTradeSystem.Core.Tests.Adapters;

public class EquityTradeAdapterTests
{
    private readonly EquityTradeAdapter _adapter;

    public EquityTradeAdapterTests()
    {
        _adapter = new EquityTradeAdapter();
    }

    [Fact]
    public async Task AdaptToEventAsync_WithValidEnvelope_ReturnsTradeCreatedEvent()
    {
        // Arrange
        var equityMessage = new EquityTradeMessage
        {
            TradeId = "EQUITY001",
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
            Sector = "Technology",
            DividendRate = 2.5m,
            Isin = "US0378331005",
            MarketSegment = "MAIN"
        };

        var envelope = new TradeMessageEnvelope<EquityTradeMessage>
        {
            MessageId = "MSG001",
            Timestamp = DateTime.UtcNow,
            Version = "1.0",
            CorrelationId = "CORR001",
            Payload = equityMessage
        };

        // Act
        var result = await _adapter.AdaptToEventAsync(envelope, "CORR001");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("EQUITY001", result.AggregateId);
        Assert.Equal("TRADER001", result.TraderId);
        Assert.Equal("AAPL", result.InstrumentId);
        Assert.Equal(100, result.Quantity);
        Assert.Equal(150.50m, result.Price);
        Assert.Equal("BUY", result.Direction);
        Assert.Equal("USD", result.Currency);
        Assert.Equal("CP001", result.CounterpartyId);
        Assert.Equal("EQUITY", result.TradeType);
        Assert.Equal("CORR001", result.CorrelationId);
        Assert.Equal("KafkaConsumerService", result.CausedBy);
        
        Assert.Contains("Symbol", result.AdditionalData.Keys);
        Assert.Equal("AAPL", result.AdditionalData["Symbol"]);
        Assert.Contains("Exchange", result.AdditionalData.Keys);
        Assert.Equal("NASDAQ", result.AdditionalData["Exchange"]);
        Assert.Contains("DividendRate", result.AdditionalData.Keys);
        Assert.Equal(2.5m, result.AdditionalData["DividendRate"]);
    }

    [Fact]
    public async Task AdaptToEventAsync_WithNullEnvelope_ReturnsNull()
    {
        // Act
        var result = await _adapter.AdaptToEventAsync(null!, "CORR001");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AdaptToEventAsync_WithNullPayload_ReturnsNull()
    {
        // Arrange
        var envelope = new TradeMessageEnvelope<EquityTradeMessage>
        {
            MessageId = "MSG001",
            Payload = null!
        };

        // Act
        var result = await _adapter.AdaptToEventAsync(envelope, "CORR001");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("EQUITY_SYSTEM", "EQUITY", true)]
    [InlineData("", "EQUITY", true)]
    [InlineData("OTHER_SYSTEM", "EQUITY", false)]
    [InlineData("EQUITY_SYSTEM", "FX", false)]
    [InlineData("EQUITY_SYSTEM", "equity", true)]
    public void CanHandle_WithVariousInputs_ReturnsExpectedResult(string sourceSystem, string messageType, bool expected)
    {
        // Act
        var result = _adapter.CanHandle(sourceSystem, messageType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Properties_HaveCorrectValues()
    {
        // Assert
        Assert.Equal("EQUITY_SYSTEM", _adapter.SourceSystem);
        Assert.Equal("EQUITY", _adapter.MessageType);
    }
}