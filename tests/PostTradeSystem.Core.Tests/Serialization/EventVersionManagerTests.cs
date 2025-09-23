using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Serialization.Contracts;
using Xunit;

namespace PostTradeSystem.Core.Tests.Serialization;

public class EventVersionManagerTests
{
    private readonly EventVersionManager _versionManager;

    public EventVersionManagerTests()
    {
        _versionManager = new EventVersionManager();
        SetupTestContracts();
    }

    [Fact]
    public void CanHandle_WithRegisteredEventType_ReturnsTrue()
    {
        var result = _versionManager.CanHandle("TradeCreated", 1);

        Assert.True(result);
    }

    [Fact]
    public void CanHandle_WithUnregisteredEventType_ReturnsFalse()
    {
        var result = _versionManager.CanHandle("UnknownEvent", 1);

        Assert.False(result);
    }

    [Fact]
    public void GetSupportedVersions_WithRegisteredEventType_ReturnsVersions()
    {
        var result = _versionManager.GetSupportedVersions("TradeCreated");

        Assert.Contains(1, result);
        Assert.Contains(2, result);
    }

    [Fact]
    public void GetSupportedVersions_WithUnregisteredEventType_ReturnsEmpty()
    {
        var result = _versionManager.GetSupportedVersions("UnknownEvent");

        Assert.Empty(result);
    }

    [Fact]
    public void GetLatestVersion_WithRegisteredEventType_ReturnsLatestVersion()
    {
        var result = _versionManager.GetLatestVersion("TradeCreated");

        Assert.Equal(2, result);
    }

    [Fact]
    public void GetLatestVersion_WithUnregisteredEventType_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => _versionManager.GetLatestVersion("UnknownEvent"));
    }

    [Fact]
    public void GetContractType_WithValidEventTypeAndVersion_ReturnsType()
    {
        var result = _versionManager.GetContractType("TradeCreated", 1);

        Assert.Equal(typeof(TradeCreatedEventV1), result);
    }

    [Fact]
    public void GetContractType_WithInvalidEventTypeOrVersion_ReturnsNull()
    {
        var result = _versionManager.GetContractType("UnknownEvent", 1);

        Assert.Null(result);
    }

    private void SetupTestContracts()
    {
        _versionManager.Register<TradeCreatedEventV1>("TradeCreated",
            domainEvent => new TradeCreatedEventV1(),
            contract => new TradeCreatedEvent(
                Guid.NewGuid().ToString(),
                "trader1",
                "instrument1",
                100,
                50.0m,
                "BUY",
                DateTime.UtcNow,
                "USD",
                "counterparty1",
                "EQUITY",
                1,
                Guid.NewGuid().ToString(),
                "system",
                new Dictionary<string, object>()));

        _versionManager.Register<TradeCreatedEventV2>("TradeCreated",
            domainEvent => new TradeCreatedEventV2(),
            contract => new TradeCreatedEvent(
                Guid.NewGuid().ToString(),
                "trader1",
                "instrument1",
                100,
                50.0m,
                "BUY",
                DateTime.UtcNow,
                "USD",
                "counterparty1",
                "EQUITY",
                1,
                Guid.NewGuid().ToString(),
                "system",
                new Dictionary<string, object>()));
    }
}