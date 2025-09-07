using PostTradeSystem.Core.Commands;
using Xunit;

namespace PostTradeSystem.Core.Tests.Commands;

public class TradeCommandsTests
{
    [Fact]
    public void CreateTradeCommand_ShouldInitializeAllProperties()
    {
        var tradeId = "TRADE-001";
        var traderId = "TRADER-001";
        var instrumentId = "AAPL";
        var quantity = 100m;
        var price = 150.50m;
        var direction = "BUY";
        var tradeDateTime = DateTime.UtcNow;
        var currency = "USD";
        var counterpartyId = "COUNTERPARTY-001";
        var tradeType = "EQUITY";
        var additionalData = new Dictionary<string, object> { ["key"] = "value" };
        var correlationId = "CORR-001";
        var causedBy = "SYSTEM";

        var command = new CreateTradeCommand(
            tradeId, traderId, instrumentId, quantity, price, direction,
            tradeDateTime, currency, counterpartyId, tradeType, additionalData,
            correlationId, causedBy);

        Assert.Equal(tradeId, command.AggregateId);
        Assert.Equal(traderId, command.TraderId);
        Assert.Equal(instrumentId, command.InstrumentId);
        Assert.Equal(quantity, command.Quantity);
        Assert.Equal(price, command.Price);
        Assert.Equal(direction, command.Direction);
        Assert.Equal(tradeDateTime, command.TradeDateTime);
        Assert.Equal(currency, command.Currency);
        Assert.Equal(counterpartyId, command.CounterpartyId);
        Assert.Equal(tradeType, command.TradeType);
        Assert.Equal(additionalData, command.AdditionalData);
        Assert.Equal(correlationId, command.CorrelationId);
        Assert.Equal(causedBy, command.CausedBy);
        Assert.NotEmpty(command.CommandId);
        Assert.True(command.IssuedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void UpdateTradeStatusCommand_ShouldInitializeAllProperties()
    {
        var tradeId = "TRADE-001";
        var newStatus = "EXECUTED";
        var reason = "Trade executed successfully";
        var correlationId = "CORR-001";
        var causedBy = "SYSTEM";

        var command = new UpdateTradeStatusCommand(tradeId, newStatus, reason, correlationId, causedBy);

        Assert.Equal(tradeId, command.AggregateId);
        Assert.Equal(newStatus, command.NewStatus);
        Assert.Equal(reason, command.Reason);
        Assert.Equal(correlationId, command.CorrelationId);
        Assert.Equal(causedBy, command.CausedBy);
        Assert.NotEmpty(command.CommandId);
    }

    [Fact]
    public void EnrichTradeCommand_ShouldInitializeAllProperties()
    {
        var tradeId = "TRADE-001";
        var enrichmentType = "RISK_DATA";
        var enrichmentData = new Dictionary<string, object> { ["riskScore"] = 85 };
        var correlationId = "CORR-001";
        var causedBy = "RISK_ENGINE";

        var command = new EnrichTradeCommand(tradeId, enrichmentType, enrichmentData, correlationId, causedBy);

        Assert.Equal(tradeId, command.AggregateId);
        Assert.Equal(enrichmentType, command.EnrichmentType);
        Assert.Equal(enrichmentData, command.EnrichmentData);
        Assert.Equal(correlationId, command.CorrelationId);
        Assert.Equal(causedBy, command.CausedBy);
    }

    [Fact]
    public void ValidateTradeCommand_ShouldInitializeAllProperties()
    {
        var tradeId = "TRADE-001";
        var correlationId = "CORR-001";
        var causedBy = "VALIDATION_SERVICE";

        var command = new ValidateTradeCommand(tradeId, correlationId, causedBy);

        Assert.Equal(tradeId, command.AggregateId);
        Assert.Equal(correlationId, command.CorrelationId);
        Assert.Equal(causedBy, command.CausedBy);
    }

    [Fact]
    public void CommandBase_ShouldGenerateUniqueCommandIds()
    {
        var command1 = new ValidateTradeCommand("TRADE-001", "CORR-001", "SYSTEM");
        var command2 = new ValidateTradeCommand("TRADE-002", "CORR-002", "SYSTEM");

        Assert.NotEqual(command1.CommandId, command2.CommandId);
    }
}