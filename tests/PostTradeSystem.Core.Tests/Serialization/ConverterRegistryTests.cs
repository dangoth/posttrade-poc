using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Serialization.Contracts;
using Xunit;

namespace PostTradeSystem.Core.Tests.Serialization;

public class ConverterRegistryTests
{
    private readonly ConverterRegistry _registry;

    public ConverterRegistryTests()
    {
        _registry = new ConverterRegistry();
    }

    [Fact]
    public void RegisterConverter_WithValidConverter_RegistersSuccessfully()
    {
        var converter = new TradeCreatedEventV1ToV2Converter();

        _registry.RegisterConverter<TradeCreatedEventV1, TradeCreatedEventV2>(converter);

        Assert.True(_registry.CanConvert<TradeCreatedEventV1, TradeCreatedEventV2>());
    }

    [Fact]
    public void Convert_WithRegisteredConverter_ConvertsSuccessfully()
    {
        var converter = new TradeCreatedEventV1ToV2Converter();
        _registry.RegisterConverter<TradeCreatedEventV1, TradeCreatedEventV2>(converter);

        var source = new TradeCreatedEventV1
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = Guid.NewGuid().ToString(),
            TraderId = "trader1",
            InstrumentId = "instrument1",
            Quantity = 100,
            Price = 50.0m,
            Direction = "BUY",
            TradeDateTime = DateTime.UtcNow,
            Currency = "USD",
            CounterpartyId = "counterparty1",
            TradeType = "EQUITY",
            AdditionalData = new Dictionary<string, object>()
        };

        var result = _registry.Convert<TradeCreatedEventV1, TradeCreatedEventV2>(source);

        Assert.NotNull(result);
        Assert.Equal(2, result.SchemaVersion);
        Assert.Equal(source.TraderId, result.TraderId);
    }

    [Fact]
    public void Convert_WithoutRegisteredConverter_ThrowsException()
    {
        var source = new TradeCreatedEventV1();

        Assert.Throws<InvalidOperationException>(() => 
            _registry.Convert<TradeCreatedEventV1, TradeCreatedEventV2>(source));
    }

    [Fact]
    public void CanConvert_WithoutRegisteredConverter_ReturnsFalse()
    {
        var result = _registry.CanConvert<TradeCreatedEventV1, TradeCreatedEventV2>();

        Assert.False(result);
    }
}