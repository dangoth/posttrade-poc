using PostTradeSystem.Core.Conventions;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Commands;
using Xunit;

namespace PostTradeSystem.Core.Tests.Conventions;

public class EventNamingConventionsTests
{
    [Fact]
    public void EventTypes_ShouldHaveCorrectConstants()
    {
        Assert.Equal("TradeCreated", EventNamingConventions.EventTypes.TRADE_CREATED);
        Assert.Equal("TradeStatusChanged", EventNamingConventions.EventTypes.TRADE_STATUS_CHANGED);
        Assert.Equal("TradeUpdated", EventNamingConventions.EventTypes.TRADE_UPDATED);
        Assert.Equal("TradeEnriched", EventNamingConventions.EventTypes.TRADE_ENRICHED);
        Assert.Equal("TradeValidationFailed", EventNamingConventions.EventTypes.TRADE_VALIDATION_FAILED);
    }

    [Fact]
    public void CommandTypes_ShouldHaveCorrectConstants()
    {
        Assert.Equal("CreateTrade", EventNamingConventions.CommandTypes.CREATE_TRADE);
        Assert.Equal("UpdateTradeStatus", EventNamingConventions.CommandTypes.UPDATE_TRADE_STATUS);
        Assert.Equal("EnrichTrade", EventNamingConventions.CommandTypes.ENRICH_TRADE);
        Assert.Equal("ValidateTrade", EventNamingConventions.CommandTypes.VALIDATE_TRADE);
    }

    [Fact]
    public void Topics_ShouldHaveCorrectConstants()
    {
        Assert.Equal("trades.equities", EventNamingConventions.Topics.TRADES_EQUITIES);
        Assert.Equal("trades.options", EventNamingConventions.Topics.TRADES_OPTIONS);
        Assert.Equal("trades.fx", EventNamingConventions.Topics.TRADES_FX);
        Assert.Equal("reference.instruments", EventNamingConventions.Topics.REFERENCE_INSTRUMENTS);
        Assert.Equal("reference.traders", EventNamingConventions.Topics.REFERENCE_TRADERS);
    }

    [Theory]
    [InlineData("EQUITY", "trades.equities")]
    [InlineData("OPTION", "trades.options")]
    [InlineData("FX", "trades.fx")]
    public void GetTopicForTradeType_ShouldReturnCorrectTopic(string tradeType, string expectedTopic)
    {
        var topic = EventNamingConventions.Topics.GetTopicForTradeType(tradeType);

        Assert.Equal(expectedTopic, topic);
    }

    [Fact]
    public void GetTopicForTradeType_WithInvalidType_ShouldThrowException()
    {
        Assert.Throws<ArgumentException>(() => 
            EventNamingConventions.Topics.GetTopicForTradeType("INVALID"));
    }

    [Fact]
    public void GetTradePartitionKey_ShouldReturnCorrectFormat()
    {
        var tradeId = "TRADE-001";
        var partitionKey = EventNamingConventions.PartitionKeys.GetTradePartitionKey(tradeId);

        Assert.Equal("trade-TRADE-001", partitionKey);
    }

    [Fact]
    public void GetTraderPartitionKey_ShouldReturnCorrectFormat()
    {
        var traderId = "TRADER-001";
        var partitionKey = EventNamingConventions.PartitionKeys.GetTraderPartitionKey(traderId);

        Assert.Equal("trader-TRADER-001", partitionKey);
    }

    [Fact]
    public void GetInstrumentPartitionKey_ShouldReturnCorrectFormat()
    {
        var instrumentId = "AAPL";
        var partitionKey = EventNamingConventions.PartitionKeys.GetInstrumentPartitionKey(instrumentId);

        Assert.Equal("instrument-AAPL", partitionKey);
    }

    [Fact]
    public void GetEventTypeName_ShouldRemoveEventSuffix()
    {
        var eventType = typeof(TradeCreatedEvent);
        var typeName = EventNamingConventions.GetEventTypeName(eventType);

        Assert.Equal("TradeCreated", typeName);
    }

    [Fact]
    public void GetCommandTypeName_ShouldRemoveCommandSuffix()
    {
        var commandType = typeof(CreateTradeCommand);
        var typeName = EventNamingConventions.GetCommandTypeName(commandType);

        Assert.Equal("CreateTrade", typeName);
    }

    [Fact]
    public void GenerateCorrelationId_ShouldIncludeStrategyAndIdentifier()
    {
        var strategy = EventNamingConventions.CorrelationStrategies.TRADE_LIFECYCLE;
        var identifier = "TRADE-001";
        
        var correlationId = EventNamingConventions.GenerateCorrelationId(strategy, identifier);

        Assert.StartsWith($"{strategy}-{identifier}-", correlationId);
        Assert.True(correlationId.Length > strategy.Length + identifier.Length + 2);
    }

    [Fact]
    public void Headers_ShouldHaveCorrectConstants()
    {
        Assert.Equal("event-type", EventNamingConventions.Headers.EVENT_TYPE);
        Assert.Equal("event-version", EventNamingConventions.Headers.EVENT_VERSION);
        Assert.Equal("correlation-id", EventNamingConventions.Headers.CORRELATION_ID);
        Assert.Equal("causation-id", EventNamingConventions.Headers.CAUSATION_ID);
        Assert.Equal("aggregate-id", EventNamingConventions.Headers.AGGREGATE_ID);
        Assert.Equal("aggregate-type", EventNamingConventions.Headers.AGGREGATE_TYPE);
        Assert.Equal("aggregate-version", EventNamingConventions.Headers.AGGREGATE_VERSION);
        Assert.Equal("source-system", EventNamingConventions.Headers.SOURCE_SYSTEM);
        Assert.Equal("timestamp", EventNamingConventions.Headers.TIMESTAMP);
        Assert.Equal("schema-version", EventNamingConventions.Headers.SCHEMA_VERSION);
    }
}