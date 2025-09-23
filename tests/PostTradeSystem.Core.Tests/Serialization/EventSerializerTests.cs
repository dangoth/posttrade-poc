using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Serialization.Contracts;
using Moq;
using Xunit;

namespace PostTradeSystem.Core.Tests.Serialization;

public class EventSerializerTests
{
    private readonly Mock<IEventVersionManager> _mockVersionManager;
    private readonly Mock<ISchemaRegistry> _mockSchemaRegistry;
    private readonly Mock<JsonSchemaValidator> _mockValidator;
    private readonly EventSerializer _serializer;

    public EventSerializerTests()
    {
        _mockVersionManager = new Mock<IEventVersionManager>();
        _mockSchemaRegistry = new Mock<ISchemaRegistry>();
        _mockValidator = new Mock<JsonSchemaValidator>();

        _serializer = new EventSerializer(
            _mockVersionManager.Object,
            _mockSchemaRegistry.Object,
            _mockValidator.Object);
    }

    [Fact]
    public async Task SerializeAsync_WithValidEvent_ReturnsSerializedEvent()
    {
        var domainEvent = CreateTestEvent();
        var contract = new TradeCreatedEventV1();
        
        _mockVersionManager.Setup(x => x.GetLatestVersion("TradeCreated")).Returns(1);
        _mockVersionManager.Setup(x => x.GetContractType("TradeCreated", 1)).Returns(typeof(TradeCreatedEventV1));
        _mockVersionManager.Setup(x => x.ConvertFromDomainEvent(domainEvent, 1)).Returns(contract);
        _mockSchemaRegistry.Setup(x => x.GetLatestSchemaAsync(It.IsAny<string>()))
            .ReturnsAsync(new SchemaMetadata(1, 1, "{}", "test-subject"));

        var result = await _serializer.SerializeAsync(domainEvent);

        Assert.NotNull(result);
        Assert.Equal("TradeCreated", result.EventType);
        Assert.Equal(1, result.SchemaVersion);
        _mockVersionManager.Verify(x => x.ConvertFromDomainEvent(domainEvent, 1), Times.Once);
    }

    [Fact]
    public async Task SerializeAsync_WithTargetVersion_UsesSpecifiedVersion()
    {
        var domainEvent = CreateTestEvent();
        var contract = new TradeCreatedEventV2();
        
        _mockVersionManager.Setup(x => x.GetContractType("TradeCreated", 2)).Returns(typeof(TradeCreatedEventV2));
        _mockVersionManager.Setup(x => x.ConvertFromDomainEvent(domainEvent, 2)).Returns(contract);
        _mockSchemaRegistry.Setup(x => x.GetLatestSchemaAsync(It.IsAny<string>()))
            .ReturnsAsync(new SchemaMetadata(1, 2, "{}", "test-subject"));

        var result = await _serializer.SerializeAsync(domainEvent, 2);

        Assert.Equal(2, result.SchemaVersion);
        _mockVersionManager.Verify(x => x.ConvertFromDomainEvent(domainEvent, 2), Times.Once);
    }

    [Fact]
    public async Task DeserializeAsync_WithValidSerializedEvent_ReturnsDomainEvent()
    {
        var serializedEvent = new SerializedEvent("TradeCreated", 1, "{}", "schema1", DateTime.UtcNow, new Dictionary<string, string>());
        var contract = new TradeCreatedEventV1();
        var domainEvent = CreateTestEvent();
        
        _mockVersionManager.Setup(x => x.GetContractType("TradeCreated", 1)).Returns(typeof(TradeCreatedEventV1));
        _mockVersionManager.Setup(x => x.GetLatestVersion("TradeCreated")).Returns(1);
        _mockVersionManager.Setup(x => x.ConvertToDomainEvent(It.IsAny<IVersionedEventContract>())).Returns(domainEvent);

        var result = await _serializer.DeserializeAsync(serializedEvent);

        Assert.Equal(domainEvent, result);
        _mockVersionManager.Verify(x => x.ConvertToDomainEvent(It.IsAny<IVersionedEventContract>()), Times.Once);
    }

    [Fact]
    public async Task SerializeAsync_WithUnregisteredEventType_ThrowsException()
    {
        var domainEvent = CreateTestEvent();
        
        _mockVersionManager.Setup(x => x.GetLatestVersion("TradeCreated")).Returns(1);
        _mockVersionManager.Setup(x => x.GetContractType("TradeCreated", 1)).Returns((Type)null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _serializer.SerializeAsync(domainEvent));
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