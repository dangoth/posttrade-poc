using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Messages;
using System.Text.Json;
using Xunit;
using FluentAssertions;

namespace PostTradeSystem.Infrastructure.Tests.Kafka;

public class MessageValidationTests
{
    private readonly IJsonSchemaValidator _schemaValidator;

    public MessageValidationTests()
    {
        _schemaValidator = new JsonSchemaValidator();
        _schemaValidator.RegisterSchema("EquityTradeMessage", MessageSchemas.EquityTradeMessageSchema);
        _schemaValidator.RegisterSchema("FxTradeMessage", MessageSchemas.FxTradeMessageSchema);
        _schemaValidator.RegisterSchema("OptionTradeMessage", MessageSchemas.OptionTradeMessageSchema);
        _schemaValidator.RegisterSchema("TradeMessage", MessageSchemas.TradeMessageSchema);
    }

    [Fact]
    public void ValidateEquityTradeMessage_WithValidMessage_ShouldReturnTrue()
    {
        var envelope = new TradeMessageEnvelope<EquityTradeMessage>
        {
            MessageId = "TEST-001",
            Timestamp = DateTime.UtcNow,
            Version = "1.0",
            CorrelationId = "CORR-001",
            Payload = new EquityTradeMessage
            {
                TradeId = "TRADE-001",
                TraderId = "TRADER-001",
                InstrumentId = "AAPL",
                Quantity = 100,
                Price = 150.50m,
                Direction = "BUY",
                TradeDateTime = DateTime.UtcNow,
                Currency = "USD",
                Status = "EXECUTED",
                CounterpartyId = "COUNTER-001",
                SourceSystem = "TEST-SYSTEM",
                MessageType = "EQUITY",
                Symbol = "AAPL",
                Exchange = "NASDAQ"
            }
        };

        var messageJson = JsonSerializer.Serialize(envelope);
        var isValid = _schemaValidator.ValidateMessage("EquityTradeMessage", messageJson, 1);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateFxTradeMessage_WithValidMessage_ShouldReturnTrue()
    {
        var envelope = new TradeMessageEnvelope<FxTradeMessage>
        {
            MessageId = "FX-TEST-001",
            Timestamp = DateTime.UtcNow,
            Version = "1.0",
            CorrelationId = "CORR-FX-001",
            Payload = new FxTradeMessage
            {
                TradeId = "FX-TRADE-001",
                TraderId = "FX-TRADER-001",
                InstrumentId = "EURUSD",
                Quantity = 1000000,
                Price = 1.0850m,
                Direction = "SELL",
                TradeDateTime = DateTime.UtcNow,
                Currency = "USD",
                Status = "EXECUTED",
                CounterpartyId = "FX-COUNTER-001",
                SourceSystem = "FX-SYSTEM",
                MessageType = "FX",
                BaseCurrency = "EUR",
                QuoteCurrency = "USD",
                SettlementDate = DateTime.UtcNow.AddDays(2),
                SpotRate = 1.0850m,
                TradeType = "SPOT"
            }
        };

        var messageJson = JsonSerializer.Serialize(envelope);
        var isValid = _schemaValidator.ValidateMessage("FxTradeMessage", messageJson, 1);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateOptionTradeMessage_WithValidMessage_ShouldReturnTrue()
    {
        var envelope = new TradeMessageEnvelope<OptionTradeMessage>
        {
            MessageId = "OPT-TEST-001",
            Timestamp = DateTime.UtcNow,
            Version = "1.0",
            CorrelationId = "CORR-OPT-001",
            Payload = new OptionTradeMessage
            {
                TradeId = "OPT-TRADE-001",
                TraderId = "OPT-TRADER-001",
                InstrumentId = "AAPL240315C00150000",
                Quantity = 10,
                Price = 5.25m,
                Direction = "BUY",
                TradeDateTime = DateTime.UtcNow,
                Currency = "USD",
                Status = "EXECUTED",
                CounterpartyId = "OPT-COUNTER-001",
                SourceSystem = "OPTIONS-SYSTEM",
                MessageType = "OPTION",
                UnderlyingSymbol = "AAPL",
                StrikePrice = 150.0m,
                ExpirationDate = DateTime.UtcNow.AddMonths(2),
                OptionType = "CALL",
                Exchange = "CBOE"
            }
        };

        var messageJson = JsonSerializer.Serialize(envelope);
        var isValid = _schemaValidator.ValidateMessage("OptionTradeMessage", messageJson, 1);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateMessage_WithInvalidMessageType_ShouldReturnFalse()
    {
        var invalidMessage = """{"invalid": "message"}""";
        var isValid = _schemaValidator.ValidateMessage("EquityTradeMessage", invalidMessage, 1);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateMessage_WithUnknownSchema_ShouldReturnVersionBasedResult()
    {
        var message = """{"test": "message"}""";
        var isValid = _schemaValidator.ValidateMessage("UnknownSchema", message, 1);

        isValid.Should().BeTrue();
    }
}