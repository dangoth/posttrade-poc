using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Infrastructure.Schemas;
using PostTradeSystem.Infrastructure.Serialization;
using System.Text;
using Xunit;
using FluentAssertions;

namespace PostTradeSystem.Infrastructure.Tests.Serialization;

public class KafkaEventSerializationTests
{
    private readonly EventSerializationRegistry _registry;
    private readonly ISchemaRegistry _schemaRegistry;
    private readonly JsonSchemaValidator _validator;
    private readonly JsonEventSerializer _eventSerializer;
    private readonly KafkaEventSerializer _kafkaSerializer;

    public KafkaEventSerializationTests()
    {
        _registry = new EventSerializationRegistry();
        _schemaRegistry = new InMemorySchemaRegistry();
        _validator = new JsonSchemaValidator();
        _eventSerializer = new JsonEventSerializer(_registry, _schemaRegistry, _validator);
        _kafkaSerializer = new KafkaEventSerializer(_eventSerializer, _schemaRegistry);
        
        EventSerializationConfiguration.Configure(_registry);
        
        var schemaRegistrationService = new SchemaRegistrationService(_schemaRegistry, _validator);
        schemaRegistrationService.RegisterEventContractSchemasAsync().Wait();
    }

    [Fact]
    public async Task SerializeForKafka_TradeCreatedEvent_ShouldIncludeHeaders()
    {
        var domainEvent = CreateSampleTradeCreatedEvent();

        var kafkaMessage = await _kafkaSerializer.SerializeForKafkaAsync(domainEvent);

        kafkaMessage.Should().NotBeNull();
        kafkaMessage.Key.Should().Be(domainEvent.AggregateId);
        kafkaMessage.Topic.Should().Be("trades.equities");
        kafkaMessage.Partition.Should().BeInRange(0, 2);

        kafkaMessage.Headers.Should().ContainKey("event-type");
        kafkaMessage.Headers.Should().ContainKey("event-version");
        kafkaMessage.Headers.Should().ContainKey("correlation-id");
        kafkaMessage.Headers.Should().ContainKey("caused-by");
        kafkaMessage.Headers.Should().ContainKey("aggregate-version");

        GetHeaderValue(kafkaMessage.Headers, "event-type").Should().Be("TradeCreated");
        GetHeaderValue(kafkaMessage.Headers, "event-version").Should().Be("2");
        GetHeaderValue(kafkaMessage.Headers, "correlation-id").Should().Be(domainEvent.CorrelationId);
        GetHeaderValue(kafkaMessage.Headers, "caused-by").Should().Be(domainEvent.CausedBy);
    }

    [Fact]
    public async Task DeserializeFromKafka_ShouldRecreateOriginalEvent()
    {
        var originalEvent = CreateSampleTradeCreatedEvent();
        var kafkaMessage = await _kafkaSerializer.SerializeForKafkaAsync(originalEvent);

        var deserializedEvent = _kafkaSerializer.DeserializeFromKafka(kafkaMessage);

        var tradeEvent = deserializedEvent.Should().BeOfType<TradeCreatedEvent>().Subject;
        tradeEvent.AggregateId.Should().Be(originalEvent.AggregateId);
        tradeEvent.TraderId.Should().Be(originalEvent.TraderId);
        tradeEvent.InstrumentId.Should().Be(originalEvent.InstrumentId);
        tradeEvent.Quantity.Should().Be(originalEvent.Quantity);
        tradeEvent.Price.Should().Be(originalEvent.Price);
    }

    [Fact]
    public async Task CanDeserialize_ValidKafkaMessage_ShouldReturnTrue()
    {
        var domainEvent = CreateSampleTradeCreatedEvent();
        var kafkaMessage = await _kafkaSerializer.SerializeForKafkaAsync(domainEvent);

        var canDeserialize = _kafkaSerializer.CanDeserialize(kafkaMessage);

        canDeserialize.Should().BeTrue();
    }

    [Fact]
    public void CanDeserialize_InvalidKafkaMessage_ShouldReturnFalse()
    {
        var invalidMessage = new KafkaMessage(
            "test-key",
            "invalid-json",
            new Dictionary<string, byte[]>(),
            0,
            "test-topic");

        var canDeserialize = _kafkaSerializer.CanDeserialize(invalidMessage);

        canDeserialize.Should().BeFalse();
    }

    [Fact]
    public async Task PartitionCalculation_SameAggregateId_ShouldReturnSamePartition()
    {
        var event1 = CreateSampleTradeCreatedEvent();
        var event2 = new TradeCreatedEvent(
            event1.AggregateId, "TRADER-002", "MSFT", 2000m, 200m,
            "SELL", DateTime.UtcNow, "USD", "COUNTER-002", "EQUITY",
            2, "CORR-002", "TestSystem", new Dictionary<string, object>());

        var message1 = await _kafkaSerializer.SerializeForKafkaAsync(event1);
        var message2 = await _kafkaSerializer.SerializeForKafkaAsync(event2);

        message1.Partition.Should().Be(message2.Partition);
    }

    [Fact]
    public async Task TopicRouting_TradeEvents_ShouldRouteToEquitiesTopic()
    {
        var tradeCreated = CreateSampleTradeCreatedEvent();
        var tradeStatusChanged = new TradeStatusChangedEvent(
            "TRADE-001", "PENDING", "EXECUTED", "Trade executed",
            2, "CORR-001", "TradingSystem");

        var createdMessage = await _kafkaSerializer.SerializeForKafkaAsync(tradeCreated);
        var statusMessage = await _kafkaSerializer.SerializeForKafkaAsync(tradeStatusChanged);

        createdMessage.Topic.Should().Be("trades.equities");
        statusMessage.Topic.Should().Be("trades.equities");
    }

    [Fact]
    public async Task MetadataHeaders_ShouldIncludeSerializationInfo()
    {
        var domainEvent = CreateSampleTradeCreatedEvent();

        var kafkaMessage = await _kafkaSerializer.SerializeForKafkaAsync(domainEvent);

        kafkaMessage.Headers.Should().ContainKey("serialized-at");
        kafkaMessage.Headers.Should().ContainKey("schema-id");

        var serializedAt = GetHeaderValue(kafkaMessage.Headers, "serialized-at");
        DateTime.TryParse(serializedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedDate).Should().BeTrue();
        parsedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }

    [Theory]
    [InlineData("TRADE-001")]
    [InlineData("TRADE-ABC-123")]
    [InlineData("TRADE-XYZ-999")]
    public async Task PartitionDistribution_DifferentAggregateIds_ShouldDistributeAcrossPartitions(string aggregateId)
    {
        var domainEvent = new TradeCreatedEvent(
            aggregateId, "TRADER-001", "AAPL", 1000m, 100m,
            "BUY", DateTime.UtcNow, "USD", "COUNTER-001", "EQUITY",
            1, "CORR-001", "TestSystem", new Dictionary<string, object>());

        var kafkaMessage = await _kafkaSerializer.SerializeForKafkaAsync(domainEvent);

        kafkaMessage.Partition.Should().BeInRange(0, 2);
        kafkaMessage.Key.Should().Be(aggregateId);
    }

    private static TradeCreatedEvent CreateSampleTradeCreatedEvent()
    {
        return new TradeCreatedEvent(
            "TRADE-001", "TRADER-001", "AAPL", 1000m, 150m,
            "BUY", DateTime.UtcNow, "USD", "COUNTER-001", "EQUITY",
            1, "CORR-001", "TestSystem", 
            new Dictionary<string, object> { ["source"] = "Bloomberg" });
    }

    private static string GetHeaderValue(Dictionary<string, byte[]> headers, string key)
    {
        return headers.TryGetValue(key, out var value) 
            ? Encoding.UTF8.GetString(value) 
            : throw new ArgumentException($"Header '{key}' not found");
    }
}