using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Messages;
using PostTradeSystem.Infrastructure.Tests.Integration;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using System.Text.Json;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Kafka;

[Collection("SqlServer")]
public class KafkaConsumerServiceTests : IntegrationTestBase
{
    public KafkaConsumerServiceTests(SqlServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task ProcessEquityMessage_ShouldPersistEventToDatabase()
    {
        var envelope = new TradeMessageEnvelope<EquityTradeMessage>
        {
            MessageId = "TEST-EQUITY-001",
            Timestamp = DateTime.UtcNow,
            Version = "1.0",
            CorrelationId = "CORR-001",
            Payload = new EquityTradeMessage
            {
                TradeId = "TRADE-001",
                TraderId = "TRADER-001",
                InstrumentId = "AAPL",
                Quantity = 100,
                Price = 150.50m,
                Direction = "BUY",
                TradeDateTime = DateTime.UtcNow,
                Currency = "USD",
                Status = "EXECUTED",
                CounterpartyId = "COUNTER-001",
                SourceSystem = "TEST-SYSTEM",
                MessageType = "EQUITY",
                Symbol = "AAPL",
                Exchange = "NASDAQ",
                Sector = "Technology",
                DividendRate = 0.88m,
                Isin = "US0378331005",
                MarketSegment = "Main"
            }
        };

        var messageJson = JsonSerializer.Serialize(envelope);
        
        var schemaValidationResult = SchemaValidator.ValidateMessage("EquityTradeMessage", messageJson, 1);
        schemaValidationResult.Should().BeTrue();

        var tradeEvent = new TradeCreatedEvent(
            envelope.Payload.TradeId,
            envelope.Payload.TraderId,
            envelope.Payload.InstrumentId,
            envelope.Payload.Quantity,
            envelope.Payload.Price,
            envelope.Payload.Direction,
            envelope.Payload.TradeDateTime,
            envelope.Payload.Currency,
            envelope.Payload.CounterpartyId,
            "EQUITY",
            1,
            envelope.CorrelationId,
            "KafkaConsumerService",
            new Dictionary<string, object>
            {
                ["Symbol"] = envelope.Payload.Symbol,
                ["Exchange"] = envelope.Payload.Exchange,
                ["Sector"] = envelope.Payload.Sector,
                ["DividendRate"] = envelope.Payload.DividendRate,
                ["Isin"] = envelope.Payload.Isin,
                ["MarketSegment"] = envelope.Payload.MarketSegment,
                ["SourceSystem"] = envelope.Payload.SourceSystem
            });

        var partitionKey = $"{envelope.Payload.TraderId}:{envelope.Payload.InstrumentId}";
        
        await EventStoreRepository.SaveEventsAsync(
            tradeEvent.AggregateId,
            partitionKey,
            new[] { tradeEvent },
            0);

        var savedEvents = await Context.EventStore
            .Where(e => e.AggregateId == envelope.Payload.TradeId)
            .ToListAsync();

        savedEvents.Should().HaveCount(1);
        var savedEvent = savedEvents.First();
        savedEvent.EventType.Should().Be("TradeCreated");
        savedEvent.AggregateType.Should().Be("Trade");
        savedEvent.PartitionKey.Should().Be(partitionKey);
        savedEvent.CorrelationId.Should().Be(envelope.CorrelationId);
        savedEvent.CausedBy.Should().Be("KafkaConsumerService");
        savedEvent.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ProcessFxMessage_ShouldPersistEventToDatabase()
    {
        var envelope = new TradeMessageEnvelope<FxTradeMessage>
        {
            MessageId = "TEST-FX-001",
            Timestamp = DateTime.UtcNow,
            Version = "1.0",
            CorrelationId = "CORR-FX-001",
            Payload = new FxTradeMessage
            {
                TradeId = "FX-TRADE-001",
                TraderId = "FX-TRADER-001",
                InstrumentId = "EURUSD",
                Quantity = 1000000,
                Price = 1.0850m,
                Direction = "SELL",
                TradeDateTime = DateTime.UtcNow,
                Currency = "USD",
                Status = "EXECUTED",
                CounterpartyId = "FX-COUNTER-001",
                SourceSystem = "FX-SYSTEM",
                MessageType = "FX",
                BaseCurrency = "EUR",
                QuoteCurrency = "USD",
                SettlementDate = DateTime.UtcNow.AddDays(2),
                SpotRate = 1.0850m,
                ForwardPoints = 0.0m,
                TradeType = "SPOT",
                DeliveryMethod = "PVP"
            }
        };

        var messageJson = JsonSerializer.Serialize(envelope);
        
        var schemaValidationResult = SchemaValidator.ValidateMessage("FxTradeMessage", messageJson, 1);
        schemaValidationResult.Should().BeTrue();

        var tradeEvent = new TradeCreatedEvent(
            envelope.Payload.TradeId,
            envelope.Payload.TraderId,
            envelope.Payload.InstrumentId,
            envelope.Payload.Quantity,
            envelope.Payload.Price,
            envelope.Payload.Direction,
            envelope.Payload.TradeDateTime,
            envelope.Payload.Currency,
            envelope.Payload.CounterpartyId,
            "FX",
            1,
            envelope.CorrelationId,
            "KafkaConsumerService",
            new Dictionary<string, object>
            {
                ["BaseCurrency"] = envelope.Payload.BaseCurrency,
                ["QuoteCurrency"] = envelope.Payload.QuoteCurrency,
                ["SettlementDate"] = envelope.Payload.SettlementDate,
                ["SpotRate"] = envelope.Payload.SpotRate,
                ["ForwardPoints"] = envelope.Payload.ForwardPoints,
                ["TradeType"] = envelope.Payload.TradeType,
                ["DeliveryMethod"] = envelope.Payload.DeliveryMethod,
                ["SourceSystem"] = envelope.Payload.SourceSystem
            });

        var partitionKey = $"{envelope.Payload.TraderId}:{envelope.Payload.InstrumentId}";
        
        await EventStoreRepository.SaveEventsAsync(
            tradeEvent.AggregateId,
            partitionKey,
            new[] { tradeEvent },
            0);

        var savedEvents = await Context.EventStore
            .Where(e => e.AggregateId == envelope.Payload.TradeId)
            .ToListAsync();

        savedEvents.Should().HaveCount(1);
        var savedEvent = savedEvents.First();
        savedEvent.EventType.Should().Be("TradeCreated");
        savedEvent.AggregateType.Should().Be("Trade");
        savedEvent.PartitionKey.Should().Be(partitionKey);
        savedEvent.CorrelationId.Should().Be(envelope.CorrelationId);
        savedEvent.CausedBy.Should().Be("KafkaConsumerService");
    }

    [Fact]
    public async Task IdempotencyCheck_ShouldPreventDuplicateProcessing()
    {
        var messageKey = "test-topic:0:123";
        var requestHash = "test-hash";
        var aggregateId = "test-aggregate";

        var isDuplicate = await EventStoreRepository.CheckIdempotencyAsync(messageKey, requestHash);
        isDuplicate.Should().BeFalse();

        await EventStoreRepository.SaveIdempotencyAsync(
            messageKey,
            aggregateId,
            requestHash,
            "processed",
            TimeSpan.FromHours(24));

        var isDuplicateAfterSave = await EventStoreRepository.CheckIdempotencyAsync(messageKey, requestHash);
        isDuplicateAfterSave.Should().BeTrue();

        var idempotencyRecord = await Context.IdempotencyKeys
            .FirstOrDefaultAsync(i => i.IdempotencyKey == messageKey);

        idempotencyRecord.Should().NotBeNull();
        idempotencyRecord!.AggregateId.Should().Be(aggregateId);
        idempotencyRecord.RequestHash.Should().Be(requestHash);
        idempotencyRecord.ResponseData.Should().Be("processed");
        idempotencyRecord.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        idempotencyRecord.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CleanupExpiredIdempotencyKeys_ShouldRemoveExpiredRecords()
    {
        await EventStoreRepository.SaveIdempotencyAsync(
            "expired-key",
            "test-aggregate",
            "test-hash",
            "processed",
            TimeSpan.FromSeconds(-10));

        await EventStoreRepository.SaveIdempotencyAsync(
            "valid-key",
            "test-aggregate-2",
            "test-hash-2",
            "processed",
            TimeSpan.FromHours(24));

        var expiredCount = await Context.IdempotencyKeys
            .CountAsync(i => i.ExpiresAt <= DateTime.UtcNow);
        expiredCount.Should().Be(1);

        await EventStoreRepository.CleanupExpiredIdempotencyKeysAsync();

        var remainingCount = await Context.IdempotencyKeys.CountAsync();
        remainingCount.Should().Be(1);

        var remaining = await Context.IdempotencyKeys.FirstAsync();
        remaining.IdempotencyKey.Should().Be("valid-key");
    }

}