using PostTradeSystem.Core.Services;
using PostTradeSystem.Core.Serialization.Contracts;
using Xunit;
using Moq;

namespace PostTradeSystem.Core.Tests.Serialization;

public class ExternalDataEventConverterTests
{
    private readonly Mock<IExternalDataService> _mockExternalDataService;
    private readonly TradeCreatedEventV1ToV2Converter _tradeCreatedConverter;
    private readonly TradeStatusChangedEventV1ToV2Converter _tradeStatusConverter;

    public ExternalDataEventConverterTests()
    {
        _mockExternalDataService = new Mock<IExternalDataService>();
        _tradeCreatedConverter = new TradeCreatedEventV1ToV2Converter(_mockExternalDataService.Object);
        _tradeStatusConverter = new TradeStatusChangedEventV1ToV2Converter(_mockExternalDataService.Object);
    }

    [Fact]
    public void TradeCreatedEventV1ToV2Converter_Convert_UsesExternalDataService()
    {
        // Arrange
        var sourceEvent = new TradeCreatedEventV1
        {
            EventId = "event-123",
            AggregateId = "trade-456",
            AggregateType = "Trade",
            OccurredAt = DateTime.UtcNow,
            AggregateVersion = 1,
            CorrelationId = "corr-789",
            CausedBy = "system",
            TraderId = "TRADER001",
            InstrumentId = "AAPL",
            Quantity = 100,
            Price = 150.50m,
            Direction = "BUY",
            TradeDateTime = DateTime.UtcNow,
            Currency = "USD",
            CounterpartyId = "COUNTERPARTY001",
            TradeType = "EQUITY",
            AdditionalData = new Dictionary<string, object>()
        };

        _mockExternalDataService
            .Setup(x => x.GetRiskAssessmentScoreAsync("TRADER001", "AAPL", 15050m))
            .ReturnsAsync("LOW");

        // Act
        var result = _tradeCreatedConverter.Convert(sourceEvent);

        // Assert
        Assert.Equal("LOW", result.RiskProfile);
        Assert.Equal(15050m, result.NotionalValue);
        Assert.Equal("MiFID_II_EQUITY", result.RegulatoryClassification);
        
        // Verify external service was called
        _mockExternalDataService.Verify(
            x => x.GetRiskAssessmentScoreAsync("TRADER001", "AAPL", 15050m), 
            Times.Once);
    }

    [Fact]
    public void TradeCreatedEventV1ToV2Converter_Convert_PreservesAllOriginalFields()
    {
        // Arrange
        var sourceEvent = new TradeCreatedEventV1
        {
            EventId = "event-123",
            AggregateId = "trade-456",
            AggregateType = "Trade",
            OccurredAt = DateTime.UtcNow,
            AggregateVersion = 1,
            CorrelationId = "corr-789",
            CausedBy = "system",
            TraderId = "TRADER001",
            InstrumentId = "AAPL",
            Quantity = 100,
            Price = 150.50m,
            Direction = "BUY",
            TradeDateTime = DateTime.UtcNow,
            Currency = "USD",
            CounterpartyId = "COUNTERPARTY001",
            TradeType = "EQUITY",
            AdditionalData = new Dictionary<string, object> { ["key"] = "value" }
        };

        _mockExternalDataService
            .Setup(x => x.GetRiskAssessmentScoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync("LOW");

        // Act
        var result = _tradeCreatedConverter.Convert(sourceEvent);

        // Assert - All original fields preserved
        Assert.Equal(sourceEvent.EventId, result.EventId);
        Assert.Equal(sourceEvent.AggregateId, result.AggregateId);
        Assert.Equal(sourceEvent.AggregateType, result.AggregateType);
        Assert.Equal(sourceEvent.OccurredAt, result.OccurredAt);
        Assert.Equal(sourceEvent.AggregateVersion, result.AggregateVersion);
        Assert.Equal(sourceEvent.CorrelationId, result.CorrelationId);
        Assert.Equal(sourceEvent.CausedBy, result.CausedBy);
        Assert.Equal(sourceEvent.TraderId, result.TraderId);
        Assert.Equal(sourceEvent.InstrumentId, result.InstrumentId);
        Assert.Equal(sourceEvent.Quantity, result.Quantity);
        Assert.Equal(sourceEvent.Price, result.Price);
        Assert.Equal(sourceEvent.Direction, result.Direction);
        Assert.Equal(sourceEvent.TradeDateTime, result.TradeDateTime);
        Assert.Equal(sourceEvent.Currency, result.Currency);
        Assert.Equal(sourceEvent.CounterpartyId, result.CounterpartyId);
        Assert.Equal(sourceEvent.TradeType, result.TradeType);
        Assert.Equal(sourceEvent.AdditionalData["key"], result.AdditionalData["key"]);
    }

    [Fact]
    public void TradeStatusChangedEventV1ToV2Converter_Convert_UsesExternalDataService()
    {
        // Arrange
        var sourceEvent = new TradeStatusChangedEventV1
        {
            EventId = "event-123",
            AggregateId = "trade-456",
            AggregateType = "Trade",
            OccurredAt = DateTime.UtcNow,
            AggregateVersion = 2,
            CorrelationId = "corr-789",
            CausedBy = "TRADER001",
            PreviousStatus = "PENDING",
            NewStatus = "CONFIRMED",
            Reason = "Manual approval"
        };

        _mockExternalDataService
            .Setup(x => x.GetAccountHolderDetailsAsync("TRADER001"))
            .ReturnsAsync("RETAIL");

        // Act
        var result = _tradeStatusConverter.Convert(sourceEvent);

        // Assert
        Assert.Equal("TRADER001 (RETAIL)", result.ApprovedBy);
        Assert.Contains("Approved by: TRADER001 (RETAIL)", result.AuditTrail);
        
        // Verify external service was called
        _mockExternalDataService.Verify(
            x => x.GetAccountHolderDetailsAsync("TRADER001"), 
            Times.Once);
    }

    [Fact]
    public void TradeStatusChangedEventV1ToV2Converter_Convert_PreservesAllOriginalFields()
    {
        // Arrange
        var sourceEvent = new TradeStatusChangedEventV1
        {
            EventId = "event-123",
            AggregateId = "trade-456",
            AggregateType = "Trade",
            OccurredAt = DateTime.UtcNow,
            AggregateVersion = 2,
            CorrelationId = "corr-789",
            CausedBy = "TRADER001",
            PreviousStatus = "PENDING",
            NewStatus = "CONFIRMED",
            Reason = "Manual approval"
        };

        _mockExternalDataService
            .Setup(x => x.GetAccountHolderDetailsAsync(It.IsAny<string>()))
            .ReturnsAsync("RETAIL");

        // Act
        var result = _tradeStatusConverter.Convert(sourceEvent);

        // Assert - All original fields preserved
        Assert.Equal(sourceEvent.EventId, result.EventId);
        Assert.Equal(sourceEvent.AggregateId, result.AggregateId);
        Assert.Equal(sourceEvent.AggregateType, result.AggregateType);
        Assert.Equal(sourceEvent.OccurredAt, result.OccurredAt);
        Assert.Equal(sourceEvent.AggregateVersion, result.AggregateVersion);
        Assert.Equal(sourceEvent.CorrelationId, result.CorrelationId);
        Assert.Equal(sourceEvent.CausedBy, result.CausedBy);
        Assert.Equal(sourceEvent.PreviousStatus, result.PreviousStatus);
        Assert.Equal(sourceEvent.NewStatus, result.NewStatus);
        Assert.Equal(sourceEvent.Reason, result.Reason);
        Assert.Equal(sourceEvent.OccurredAt, result.ApprovalTimestamp);
    }

    [Theory]
    [InlineData("EQUITY", "MiFID_II_EQUITY")]
    [InlineData("OPTION", "MiFID_II_DERIVATIVE")]
    [InlineData("FX", "EMIR_FX")]
    [InlineData("UNKNOWN", "UNCLASSIFIED")]
    public void TradeCreatedEventV1ToV2Converter_Convert_SetsCorrectRegulatoryClassification(string tradeType, string expectedClassification)
    {
        // Arrange
        var sourceEvent = new TradeCreatedEventV1
        {
            TradeType = tradeType,
            TraderId = "TRADER001",
            InstrumentId = "TEST",
            Quantity = 100,
            Price = 10m
        };

        _mockExternalDataService
            .Setup(x => x.GetRiskAssessmentScoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync("LOW");

        // Act
        var result = _tradeCreatedConverter.Convert(sourceEvent);

        // Assert
        Assert.Equal(expectedClassification, result.RegulatoryClassification);
    }

    [Fact]
    public void TradeCreatedEventV1ToV2Converter_CanConvert_ReturnsCorrectVersions()
    {
        // Act & Assert
        Assert.True(_tradeCreatedConverter.CanConvert(1, 2));
        Assert.False(_tradeCreatedConverter.CanConvert(2, 1));
        Assert.False(_tradeCreatedConverter.CanConvert(1, 3));
    }

    [Fact]
    public void TradeStatusChangedEventV1ToV2Converter_CanConvert_ReturnsCorrectVersions()
    {
        // Act & Assert
        Assert.True(_tradeStatusConverter.CanConvert(1, 2));
        Assert.False(_tradeStatusConverter.CanConvert(2, 1));
        Assert.False(_tradeStatusConverter.CanConvert(1, 3));
    }
}