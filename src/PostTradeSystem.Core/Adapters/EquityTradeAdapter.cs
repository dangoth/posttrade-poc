using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Messages;

namespace PostTradeSystem.Core.Adapters;

public class EquityTradeAdapter : ITradeMessageAdapter<EquityTradeMessage>
{
    public string SourceSystem => "EQUITY_SYSTEM";
    public string MessageType => "EQUITY";

    public Task<TradeCreatedEvent?> AdaptToEventAsync(TradeMessageEnvelope<EquityTradeMessage> envelope, string correlationId)
    {
        if (envelope?.Payload == null)
            return Task.FromResult<TradeCreatedEvent?>(null);

        var additionalData = new Dictionary<string, object>
        {
            ["Symbol"] = envelope.Payload.Symbol ?? string.Empty,
            ["Exchange"] = envelope.Payload.Exchange ?? string.Empty,
            ["Sector"] = envelope.Payload.Sector ?? string.Empty,
            ["DividendRate"] = envelope.Payload.DividendRate,
            ["Isin"] = envelope.Payload.Isin ?? string.Empty,
            ["MarketSegment"] = envelope.Payload.MarketSegment ?? string.Empty,
            ["SourceSystem"] = envelope.Payload.SourceSystem ?? string.Empty
        };

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
            correlationId,
            "KafkaConsumerService",
            additionalData);

        return Task.FromResult<TradeCreatedEvent?>(tradeEvent);
    }

    public bool CanHandle(string sourceSystem, string messageType)
    {
        return (string.IsNullOrEmpty(sourceSystem) || sourceSystem == SourceSystem) && 
               messageType.ToUpperInvariant() == MessageType;
    }
}