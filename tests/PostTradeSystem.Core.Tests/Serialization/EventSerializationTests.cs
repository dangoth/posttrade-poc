using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Serialization.Contracts;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Infrastructure.Schemas;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using FluentAssertions;

namespace PostTradeSystem.Core.Tests.Serialization;

public class EventSerializationTests
{
    private readonly SerializationManagementService _serializationService;

    public EventSerializationTests()
    {
        var registry = new EventSerializationRegistry();
        var schemaRegistry = new InMemorySchemaRegistry();
        var validator = new JsonSchemaValidator();
        _serializationService = new SerializationManagementService(registry, schemaRegistry, validator);
        
        // Initialize the serialization service
        _serializationService.InitializeAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task SerializeAndDeserialize_TradeCreatedEvent_V1_ShouldRoundTrip()
    {
        var originalEvent = CreateSampleTradeCreatedEvent();

        var serialized = await _serializationService.SerializeAsync(originalEvent);
        var deserialized = _serializationService.Deserialize(serialized);

        serialized.Should().NotBeNull();
        serialized.EventType.Should().Be("TradeCreated");
        serialized.SchemaVersion.Should().Be(2);
        
        var deserializedEvent = deserialized.Should().BeOfType<TradeCreatedEvent>().Subject;
        AssertTradeCreatedEventEquals(originalEvent, deserializedEvent);
    }

    [Fact]
    public async Task SerializeAndDeserialize_TradeCreatedEvent_V2_ShouldIncludeRiskMetadata()
    {
        var originalEvent = CreateSampleTradeCreatedEvent();

        var serialized = await _serializationService.SerializeAsync(originalEvent);
        
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        var contractV2 = JsonSerializer.Deserialize<TradeCreatedEventV2>(serialized.Data, jsonOptions);
        
        contractV2.Should().NotBeNull();
        contractV2!.RiskProfile.Should().Be("STANDARD");
        contractV2.NotionalValue.Should().Be(1000000m);
        contractV2.RegulatoryClassification.Should().Be("MiFID_II_EQUITY");
    }

    [Fact]
    public void Deserialize_V1_TradeCreatedEvent_ShouldUpgradeToLatestVersion()
    {
        var v1Contract = new TradeCreatedEventV1
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = "TRADE-001",
            AggregateType = "Trade",
            OccurredAt = DateTime.UtcNow,
            AggregateVersion = 1,
            CorrelationId = "CORR-001",
            CausedBy = "TestSystem",
            TraderId = "TRADER-001",
            InstrumentId = "AAPL",
            Quantity = 1000,
            Price = 150.50m,
            Direction = "BUY",
            TradeDateTime = DateTime.UtcNow,
            Currency = "USD",
            CounterpartyId = "COUNTER-001",
            TradeType = "EQUITY",
            AdditionalData = new Dictionary<string, object> { ["source"] = "Bloomberg" }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        var v1Json = JsonSerializer.Serialize(v1Contract, jsonOptions);
        var serializedEvent = new SerializedEvent(
            "TradeCreated", 1, v1Json, "test-schema", DateTime.UtcNow, new Dictionary<string, string>());

        var deserialized = _serializationService.Deserialize(serializedEvent);

        var deserializedEvent = deserialized.Should().BeOfType<TradeCreatedEvent>().Subject;
        deserializedEvent.TraderId.Should().Be(v1Contract.TraderId);
        deserializedEvent.InstrumentId.Should().Be(v1Contract.InstrumentId);
        
        deserializedEvent.AdditionalData.Should().ContainKey("source");
        
        var sourceValue = deserializedEvent.AdditionalData["source"];
        if (sourceValue is JsonElement jsonElement)
        {
            jsonElement.GetString().Should().Be("Bloomberg");
        }
        else
        {
            sourceValue.Should().Be("Bloomberg");
        }
    }

    [Fact]
    public async Task SerializeAndDeserialize_TradeStatusChangedEvent_ShouldRoundTrip()
    {
        var originalEvent = new TradeStatusChangedEvent(
            "TRADE-001", "PENDING", "EXECUTED", "Trade executed successfully",
            2, "CORR-001", "TradingSystem");

        var serialized = await _serializationService.SerializeAsync(originalEvent);
        var deserialized = _serializationService.Deserialize(serialized);

        var deserializedEvent = deserialized.Should().BeOfType<TradeStatusChangedEvent>().Subject;
        deserializedEvent.AggregateId.Should().Be(originalEvent.AggregateId);
        deserializedEvent.PreviousStatus.Should().Be(originalEvent.PreviousStatus);
        deserializedEvent.NewStatus.Should().Be(originalEvent.NewStatus);
        deserializedEvent.Reason.Should().Be(originalEvent.Reason);
    }

    [Fact]
    public void VersionConverter_V1ToV2_ShouldAddRiskMetadata()
    {
        var v1Contract = new TradeCreatedEventV1
        {
            TraderId = "TRADER-001",
            InstrumentId = "AAPL",
            Quantity = 500,
            Price = 200m,
            TradeType = "EQUITY"
        };

        var converter = new TradeCreatedEventV1ToV2Converter();

        var v2Contract = converter.Convert(v1Contract);

        v2Contract.SchemaVersion.Should().Be(2);
        v2Contract.RiskProfile.Should().Be("STANDARD");
        v2Contract.NotionalValue.Should().Be(100000m); // 500 * 200
        v2Contract.RegulatoryClassification.Should().Be("MiFID_II_EQUITY");
        
        v2Contract.TraderId.Should().Be(v1Contract.TraderId);
        v2Contract.InstrumentId.Should().Be(v1Contract.InstrumentId);
        v2Contract.Quantity.Should().Be(v1Contract.Quantity);
        v2Contract.Price.Should().Be(v1Contract.Price);
    }

    [Fact]
    public void VersionConverter_V2ToV1_ShouldRemoveRiskMetadata()
    {
        var v2Contract = new TradeCreatedEventV2
        {
            TraderId = "TRADER-001",
            InstrumentId = "AAPL",
            Quantity = 500,
            Price = 200m,
            TradeType = "EQUITY",
            RiskProfile = "HIGH_RISK",
            NotionalValue = 100000m,
            RegulatoryClassification = "MiFID_II_EQUITY"
        };

        var converter = new TradeCreatedEventV2ToV1Converter();

        var v1Contract = converter.Convert(v2Contract);

        v1Contract.SchemaVersion.Should().Be(1);
        
        v1Contract.TraderId.Should().Be(v2Contract.TraderId);
        v1Contract.InstrumentId.Should().Be(v2Contract.InstrumentId);
        v1Contract.Quantity.Should().Be(v2Contract.Quantity);
        v1Contract.Price.Should().Be(v2Contract.Price);
        
        var v1Type = typeof(TradeCreatedEventV1);
        v1Type.GetProperty("RiskProfile").Should().BeNull();
        v1Type.GetProperty("NotionalValue").Should().BeNull();
        v1Type.GetProperty("RegulatoryClassification").Should().BeNull();
    }

    [Fact]
    public void SerializationRegistry_ShouldSupportMultipleVersions()
    {
        var tradeCreatedVersions = _serializationService.GetSupportedSchemaVersions("TradeCreated").ToList();
        tradeCreatedVersions.Should().Contain(1);
        tradeCreatedVersions.Should().Contain(2);
        _serializationService.GetLatestSchemaVersion("TradeCreated").Should().Be(2);

        var statusChangedVersions = _serializationService.GetSupportedSchemaVersions("TradeStatusChanged").ToList();
        statusChangedVersions.Should().Contain(1);
        statusChangedVersions.Should().Contain(2);
        _serializationService.GetLatestSchemaVersion("TradeStatusChanged").Should().Be(2);
    }

    [Fact]
    public async Task JsonSerialization_ShouldHandleDecimalPrecision()
    {
        var originalEvent = new TradeCreatedEvent(
            "TRADE-001", "TRADER-001", "AAPL", 1000.123456789m, 150.987654321m,
            "BUY", DateTime.UtcNow, "USD", "COUNTER-001", "EQUITY",
            1, "CORR-001", "TestSystem", new Dictionary<string, object>());

        var serialized = await _serializationService.SerializeAsync(originalEvent);
        var deserialized = _serializationService.Deserialize(serialized);

        var deserializedEvent = deserialized.Should().BeOfType<TradeCreatedEvent>().Subject;
        deserializedEvent.Quantity.Should().Be(originalEvent.Quantity);
        deserializedEvent.Price.Should().Be(originalEvent.Price);
    }

    [Fact]
    public async Task JsonSerialization_ShouldHandleDateTimePrecision()
    {
        var specificDateTime = new DateTime(2024, 1, 15, 14, 30, 45, 123, DateTimeKind.Utc);
        var originalEvent = new TradeCreatedEvent(
            "TRADE-001", "TRADER-001", "AAPL", 1000m, 150m,
            "BUY", specificDateTime, "USD", "COUNTER-001", "EQUITY",
            1, "CORR-001", "TestSystem", new Dictionary<string, object>());

        var serialized = await _serializationService.SerializeAsync(originalEvent);
        var deserialized = _serializationService.Deserialize(serialized);

        var deserializedEvent = deserialized.Should().BeOfType<TradeCreatedEvent>().Subject;
        deserializedEvent.TradeDateTime.Should().Be(specificDateTime);
    }

    [Fact]
    public async Task CrossVersionCompatibility_ShouldMaintainDataIntegrity()
    {
        var events = new List<SerializedEvent>();

        var v1Event = CreateV1SerializedEvent();
        events.Add(v1Event);

        var originalEvent = CreateSampleTradeCreatedEvent();
        var v2Event = await _serializationService.SerializeAsync(originalEvent);
        events.Add(v2Event);

        foreach (var serializedEvent in events)
        {
            var deserialized = _serializationService.Deserialize(serializedEvent);
            deserialized.Should().NotBeNull();
            deserialized.Should().BeOfType<TradeCreatedEvent>();
        }
    }

    private static TradeCreatedEvent CreateSampleTradeCreatedEvent()
    {
        return new TradeCreatedEvent(
            "TRADE-001", "TRADER-001", "AAPL", 10000m, 100m,
            "BUY", DateTime.UtcNow, "USD", "COUNTER-001", "EQUITY",
            1, "CORR-001", "TestSystem", 
            new Dictionary<string, object> { ["source"] = "Bloomberg" });
    }

    private static SerializedEvent CreateV1SerializedEvent()
    {
        var v1Contract = new TradeCreatedEventV1
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = "TRADE-V1",
            AggregateType = "Trade",
            OccurredAt = DateTime.UtcNow,
            AggregateVersion = 1,
            CorrelationId = "CORR-V1",
            CausedBy = "TestSystem",
            TraderId = "TRADER-V1",
            InstrumentId = "MSFT",
            Quantity = 2000,
            Price = 300m,
            Direction = "SELL",
            TradeDateTime = DateTime.UtcNow,
            Currency = "USD",
            CounterpartyId = "COUNTER-V1",
            TradeType = "EQUITY",
            AdditionalData = new Dictionary<string, object> { ["legacy"] = true }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        var json = JsonSerializer.Serialize(v1Contract, jsonOptions);
        return new SerializedEvent("TradeCreated", 1, json, "v1-schema", DateTime.UtcNow, 
            new Dictionary<string, string> { ["version"] = "1" });
    }

    private static void AssertTradeCreatedEventEquals(TradeCreatedEvent expected, TradeCreatedEvent actual)
    {
        actual.AggregateId.Should().Be(expected.AggregateId);
        actual.TraderId.Should().Be(expected.TraderId);
        actual.InstrumentId.Should().Be(expected.InstrumentId);
        actual.Quantity.Should().Be(expected.Quantity);
        actual.Price.Should().Be(expected.Price);
        actual.Direction.Should().Be(expected.Direction);
        actual.Currency.Should().Be(expected.Currency);
        actual.CounterpartyId.Should().Be(expected.CounterpartyId);
        actual.TradeType.Should().Be(expected.TradeType);
    }
}
