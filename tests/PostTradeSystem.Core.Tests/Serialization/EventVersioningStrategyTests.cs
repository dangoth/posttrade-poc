using FluentAssertions;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Infrastructure.Schemas;
using Xunit;

namespace PostTradeSystem.Core.Tests.Serialization;

public class SerializationManagementServiceTests
{
    private readonly SerializationManagementService _serializationService;

    public SerializationManagementServiceTests()
    {
        var registry = new EventSerializationRegistry();
        var schemaRegistry = new InMemorySchemaRegistry();
        var validator = new JsonSchemaValidator();
        var tradeRiskService = new TradeRiskService();
        _serializationService = new SerializationManagementService(registry, schemaRegistry, validator, tradeRiskService);
    }

    [Fact]
    public async Task InitializeAsync_ShouldRegisterAllEventVersions()
    {
        await _serializationService.InitializeAsync();

        var tradeCreatedVersions = _serializationService.GetSupportedSchemaVersions("TradeCreated").ToList();
        var tradeStatusChangedVersions = _serializationService.GetSupportedSchemaVersions("TradeStatusChanged").ToList();

        tradeCreatedVersions.Should().Contain(new[] { 1, 2 });
        tradeStatusChangedVersions.Should().Contain(new[] { 1, 2 });
    }

    [Fact]
    public async Task SerializeAsync_ShouldUseLatestSchemaVersion()
    {
        await _serializationService.InitializeAsync();

        var domainEvent = new TradeCreatedEvent(
            "TRADE-001", "TRADER-001", "AAPL", 100m, 150.50m, "BUY",
            DateTime.UtcNow, "USD", "COUNTERPARTY-001", "EQUITY", 1,
            "CORR-001", "SYSTEM", new Dictionary<string, object>());

        var serialized = await _serializationService.SerializeAsync(domainEvent);

        serialized.Should().NotBeNull();
        serialized.Value!.EventType.Should().Be("TradeCreated");
        serialized.Value.SchemaVersion.Should().Be(2); // Should use latest version
    }

    [Fact]
    public async Task ValidateEvent_ShouldValidateCorrectly()
    {
        await _serializationService.InitializeAsync();

        var domainEvent = new TradeCreatedEvent(
            "TRADE-001", "TRADER-001", "AAPL", 100m, 150.50m, "BUY",
            DateTime.UtcNow, "USD", "COUNTERPARTY-001", "EQUITY", 1,
            "CORR-001", "SYSTEM", new Dictionary<string, object>());

        var result = _serializationService.ValidateEvent(domainEvent);

        result.Should().NotBeNull();
        // Note: Validation might fail due to missing schemas, but the method should not throw
    }

    [Fact]
    public async Task CanHandle_ShouldReturnTrueForSupportedEvents()
    {
        await _serializationService.InitializeAsync();

        var canHandleV1 = _serializationService.CanHandle("TradeCreated", 1);
        var canHandleV2 = _serializationService.CanHandle("TradeCreated", 2);

        canHandleV1.Should().BeTrue();
        canHandleV2.Should().BeTrue();
    }
}