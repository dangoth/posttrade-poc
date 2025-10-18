using PostTradeSystem.Core.Routing;
using Xunit;

namespace PostTradeSystem.Core.Tests.Routing;

public class MessageRouterTests
{
    private readonly MessageRouter _router;

    public MessageRouterTests()
    {
        _router = new MessageRouter();
    }

    [Theory]
    [InlineData("trades.equities", "EQUITY_SYSTEM")]
    [InlineData("trades.fx", "FX_SYSTEM")]
    [InlineData("trades.options", "OPTION_SYSTEM")]
    [InlineData("unknown.topic", "UNKNOWN_SYSTEM")]
    public void DetermineSourceSystem_WithTopicMapping_ReturnsCorrectSystem(string topic, string expectedSystem)
    {
        // Act
        var result = _router.DetermineSourceSystem(topic, null);

        // Assert
        Assert.Equal(expectedSystem, result);
    }

    [Fact]
    public void DetermineSourceSystem_WithHeaderSourceSystem_ReturnsHeaderValue()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            ["sourceSystem"] = "CUSTOM_SYSTEM"
        };

        // Act
        var result = _router.DetermineSourceSystem("trades.equities", headers);

        // Assert
        Assert.Equal("CUSTOM_SYSTEM", result);
    }

    [Fact]
    public void DetermineSourceSystem_WithEmptyHeaderSourceSystem_UsesTopicMapping()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            ["sourceSystem"] = ""
        };

        // Act
        var result = _router.DetermineSourceSystem("trades.equities", headers);

        // Assert
        Assert.Equal("EQUITY_SYSTEM", result);
    }

    [Fact]
    public void DetermineSourceSystem_WithNullHeaders_UsesTopicMapping()
    {
        // Act
        var result = _router.DetermineSourceSystem("trades.fx", null);

        // Assert
        Assert.Equal("FX_SYSTEM", result);
    }

    [Fact]
    public void GetPartitionKey_WithValidInputs_ReturnsFormattedKey()
    {
        // Arrange
        var messageType = "EQUITY";
        var sourceSystem = "EQUITY_SYSTEM";
        var traderId = "TRADER001";
        var instrumentId = "AAPL";

        // Act
        var result = _router.GetPartitionKey(messageType, sourceSystem, traderId, instrumentId);

        // Assert
        Assert.Equal("EQUITY_SYSTEM:TRADER001:AAPL", result);
    }

    [Theory]
    [InlineData("FX", "FX_SYSTEM", "TRADER002", "EURUSD", "FX_SYSTEM:TRADER002:EURUSD")]
    [InlineData("OPTION", "OPTION_SYSTEM", "TRADER003", "AAPL240315C00150000", "OPTION_SYSTEM:TRADER003:AAPL240315C00150000")]
    public void GetPartitionKey_WithVariousInputs_ReturnsCorrectFormat(string messageType, string sourceSystem, string traderId, string instrumentId, string expected)
    {
        // Act
        var result = _router.GetPartitionKey(messageType, sourceSystem, traderId, instrumentId);

        // Assert
        Assert.Equal(expected, result);
    }
}