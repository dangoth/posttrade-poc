using FluentAssertions;
using Moq;
using PostTradeSystem.Core.Common;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Services;
using Xunit;

namespace PostTradeSystem.Core.Tests.Serialization;

public class EventSerializationOrchestratorTests
{
    private readonly Mock<IEventSerializer> _mockEventSerializer;
    private readonly Mock<IEventVersionManager> _mockVersionManager;
    private readonly Mock<ISchemaRegistry> _mockSchemaRegistry;
    private readonly Mock<ITradeRiskService> _mockTradeRiskService;
    private readonly EventSerializationOrchestrator _orchestrator;

    public EventSerializationOrchestratorTests()
    {
        _mockEventSerializer = new Mock<IEventSerializer>();
        _mockVersionManager = new Mock<IEventVersionManager>();
        _mockSchemaRegistry = new Mock<ISchemaRegistry>();
        _mockTradeRiskService = new Mock<ITradeRiskService>();

        _orchestrator = new EventSerializationOrchestrator(
            _mockEventSerializer.Object,
            _mockVersionManager.Object,
            _mockSchemaRegistry.Object,
            _mockTradeRiskService.Object);
    }

    [Fact]
    public async Task SerializeAsync_CallsEventSerializer()
    {
        var domainEvent = CreateTestEvent();
        var expectedResult = new SerializedEvent("TradeCreated", 1, "{}", "schema1", DateTime.UtcNow, new Dictionary<string, string>());
        
        _mockEventSerializer.Setup(x => x.SerializeAsync(domainEvent, null))
            .ReturnsAsync(Result<SerializedEvent>.Success(expectedResult));

        var result = await _orchestrator.SerializeAsync(domainEvent);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedResult);
        _mockEventSerializer.Verify(x => x.SerializeAsync(domainEvent, null), Times.Once);
    }

    [Fact]
    public async Task DeserializeAsync_CallsEventSerializer()
    {
        var serializedEvent = new SerializedEvent("TradeCreated", 1, "{}", "schema1", DateTime.UtcNow, new Dictionary<string, string>());
        var expectedResult = CreateTestEvent();
        
        _mockEventSerializer.Setup(x => x.DeserializeAsync(serializedEvent))
            .ReturnsAsync(Result<IDomainEvent>.Success(expectedResult));

        var result = await _orchestrator.DeserializeAsync(serializedEvent);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedResult);
        _mockEventSerializer.Verify(x => x.DeserializeAsync(serializedEvent), Times.Once);
    }

    [Fact]
    public void CanHandle_CallsVersionManager()
    {
        _mockVersionManager.Setup(x => x.CanHandle("TradeCreated", 1))
            .Returns(true);

        var result = _orchestrator.CanHandle("TradeCreated", 1);

        Assert.True(result);
        _mockVersionManager.Verify(x => x.CanHandle("TradeCreated", 1), Times.Once);
    }

    [Fact]
    public void GetSupportedSchemaVersions_CallsVersionManager()
    {
        var expectedVersions = new[] { 1, 2 };
        _mockVersionManager.Setup(x => x.GetSupportedVersions("TradeCreated"))
            .Returns(expectedVersions);

        var result = _orchestrator.GetSupportedSchemaVersions("TradeCreated");

        Assert.Equal(expectedVersions, result);
        _mockVersionManager.Verify(x => x.GetSupportedVersions("TradeCreated"), Times.Once);
    }

    [Fact]
    public void GetLatestSchemaVersion_CallsVersionManager()
    {
        _mockVersionManager.Setup(x => x.GetLatestVersion("TradeCreated"))
            .Returns(2);

        var result = _orchestrator.GetLatestSchemaVersion("TradeCreated");

        Assert.Equal(2, result);
        _mockVersionManager.Verify(x => x.GetLatestVersion("TradeCreated"), Times.Once);
    }

    [Fact]
    public async Task ValidateEventDataAsync_CallsSchemaRegistry()
    {
        var expectedResult = new ValidationResult(true, null);
        _mockSchemaRegistry.Setup(x => x.ValidateSchemaAsync("TradeCreated", "{}", 1))
            .ReturnsAsync(true);

        var result = await _orchestrator.ValidateEventDataAsync("TradeCreated", "{}", 1);

        Assert.True(result.IsValid);
        _mockSchemaRegistry.Verify(x => x.ValidateSchemaAsync("TradeCreated", "{}", 1), Times.Once);
    }

    [Fact]
    public void TradeRiskService_ReturnsInjectedService()
    {
        var result = _orchestrator.TradeRiskService;

        Assert.Equal(_mockTradeRiskService.Object, result);
    }

    private static TradeCreatedEvent CreateTestEvent()
    {
        return new TradeCreatedEvent(
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
            new Dictionary<string, object>());
    }
}