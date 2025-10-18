using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Messages;

namespace PostTradeSystem.Core.Adapters;

public class FxTradeAdapter : ITradeMessageAdapter<FxTradeMessage>
{
    public string SourceSystem => "FX_SYSTEM";
    public string MessageType => "FX";

    public Task<TradeCreatedEvent?> AdaptToEventAsync(TradeMessageEnvelope<FxTradeMessage> envelope, string correlationId)
    {
        if (envelope?.Payload == null)
            return Task.FromResult<TradeCreatedEvent?>(null);

        var additionalData = new Dictionary<string, object>
        {
            ["BaseCurrency"] = envelope.Payload.BaseCurrency ?? string.Empty,
            ["QuoteCurrency"] = envelope.Payload.QuoteCurrency ?? string.Empty,
            ["SettlementDate"] = envelope.Payload.SettlementDate,
            ["SpotRate"] = envelope.Payload.SpotRate,
            ["ForwardPoints"] = envelope.Payload.ForwardPoints,
            ["TradeType"] = envelope.Payload.TradeType ?? string.Empty,
            ["DeliveryMethod"] = envelope.Payload.DeliveryMethod ?? string.Empty,
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
            "FX",
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