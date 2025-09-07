using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Serialization.Contracts;
using Xunit;

namespace PostTradeSystem.Core.Tests.Serialization;

public class EventVersioningStrategyTests
{
    private readonly EventSerializationRegistry _registry;
    private readonly EventVersioningStrategy _strategy;

    public EventVersioningStrategyTests()
    {
        _registry = new EventSerializationRegistry();
        _strategy = new EventVersioningStrategy(_registry);
    }

    [Fact]
    public void ConfigureEventVersioning_ShouldRegisterAllEventVersions()
    {
        _strategy.ConfigureEventVersioning();

        var tradeCreatedVersions = _registry.GetSupportedVersions("TradeCreated").ToList();
        var tradeStatusChangedVersions = _registry.GetSupportedVersions("TradeStatusChanged").ToList();

        Assert.Contains(1, tradeCreatedVersions);
        Assert.Contains(2, tradeCreatedVersions);
        Assert.Contains(1, tradeStatusChangedVersions);
        Assert.Contains(2, tradeStatusChangedVersions);
    }

    [Fact]
    public void ConfigureEventVersioning_ShouldRegisterContractTypes()
    {
        _strategy.ConfigureEventVersioning();

        var v1ContractType = _registry.GetContractType("TradeCreated", 1);
        var v2ContractType = _registry.GetContractType("TradeCreated", 2);

        Assert.Equal(typeof(TradeCreatedEventV1), v1ContractType);
        Assert.Equal(typeof(TradeCreatedEventV2), v2ContractType);
    }

    [Fact]
    public void ConvertFromDomainEvent_ShouldConvertToV1Contract()
    {
        _strategy.ConfigureEventVersioning();

        var domainEvent = new TradeCreatedEvent(
            "TRADE-001", "TRADER-001", "AAPL", 100m, 150.50m, "BUY",
            DateTime.UtcNow, "USD", "COUNTERPARTY-001", "EQUITY", 1,
            "CORR-001", "SYSTEM", new Dictionary<string, object>());

        var contract = _registry.ConvertFromDomainEvent(domainEvent, 1);

        Assert.IsType<TradeCreatedEventV1>(contract);
        var v1Contract = (TradeCreatedEventV1)contract;
        Assert.Equal(domainEvent.AggregateId, v1Contract.AggregateId);
        Assert.Equal(domainEvent.TraderId, v1Contract.TraderId);
        Assert.Equal(domainEvent.InstrumentId, v1Contract.InstrumentId);
    }

    [Fact]
    public void ConvertFromDomainEvent_ShouldConvertToV2Contract()
    {
        _strategy.ConfigureEventVersioning();

        var domainEvent = new TradeCreatedEvent(
            "TRADE-001", "TRADER-001", "AAPL", 100m, 150.50m, "BUY",
            DateTime.UtcNow, "USD", "COUNTERPARTY-001", "EQUITY", 1,
            "CORR-001", "SYSTEM", new Dictionary<string, object>());

        var contract = _registry.ConvertFromDomainEvent(domainEvent, 2);

        Assert.IsType<TradeCreatedEventV2>(contract);
        var v2Contract = (TradeCreatedEventV2)contract;
        Assert.Equal(domainEvent.AggregateId, v2Contract.AggregateId);
        Assert.Equal(domainEvent.TraderId, v2Contract.TraderId);
        Assert.Equal(domainEvent.Quantity * domainEvent.Price, v2Contract.NotionalValue);
        Assert.Equal("MiFID_II_EQUITY", v2Contract.RegulatoryClassification);
        Assert.Equal("STANDARD", v2Contract.RiskProfile);
    }

    [Fact]
    public void ConvertToDomainEvent_ShouldConvertFromV1Contract()
    {
        _strategy.ConfigureEventVersioning();

        var contract = new TradeCreatedEventV1
        {
            AggregateId = "TRADE-001",
            TraderId = "TRADER-001",
            InstrumentId = "AAPL",
            Quantity = 100m,
            Price = 150.50m,
            Direction = "BUY",
            TradeDateTime = DateTime.UtcNow,
            Currency = "USD",
            CounterpartyId = "COUNTERPARTY-001",
            TradeType = "EQUITY",
            AggregateVersion = 1,
            CorrelationId = "CORR-001",
            CausedBy = "SYSTEM",
            AdditionalData = new Dictionary<string, object>()
        };

        var domainEvent = _registry.ConvertToDomainEvent(contract);

        Assert.IsType<TradeCreatedEvent>(domainEvent);
        var tradeEvent = (TradeCreatedEvent)domainEvent;
        Assert.Equal(contract.AggregateId, tradeEvent.AggregateId);
        Assert.Equal(contract.TraderId, tradeEvent.TraderId);
        Assert.Equal(contract.InstrumentId, tradeEvent.InstrumentId);
    }

    [Fact]
    public void ConvertVersion_ShouldConvertV1ToV2()
    {
        _strategy.ConfigureEventVersioning();

        var v1Contract = new TradeCreatedEventV1
        {
            AggregateId = "TRADE-001",
            TraderId = "TRADER-001",
            InstrumentId = "AAPL",
            Quantity = 100m,
            Price = 150.50m,
            TradeType = "EQUITY"
        };

        var v2Contract = _registry.ConvertVersion<TradeCreatedEventV1, TradeCreatedEventV2>(v1Contract);

        Assert.Equal(v1Contract.AggregateId, v2Contract.AggregateId);
        Assert.Equal(v1Contract.TraderId, v2Contract.TraderId);
        Assert.Equal(v1Contract.Quantity * v1Contract.Price, v2Contract.NotionalValue);
        Assert.Equal("MiFID_II_EQUITY", v2Contract.RegulatoryClassification);
        Assert.Equal("STANDARD", v2Contract.RiskProfile);
    }
}