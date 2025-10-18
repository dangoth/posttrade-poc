using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Messages;

namespace PostTradeSystem.Core.Adapters;

public class OptionTradeAdapter : ITradeMessageAdapter<OptionTradeMessage>
{
    public string SourceSystem => "OPTION_SYSTEM";
    public string MessageType => "OPTION";

    public Task<TradeCreatedEvent?> AdaptToEventAsync(TradeMessageEnvelope<OptionTradeMessage> envelope, string correlationId)
    {
        if (envelope?.Payload == null)
            return Task.FromResult<TradeCreatedEvent?>(null);

        var additionalData = new Dictionary<string, object>
        {
            ["UnderlyingSymbol"] = envelope.Payload.UnderlyingSymbol ?? string.Empty,
            ["StrikePrice"] = envelope.Payload.StrikePrice,
            ["ExpirationDate"] = envelope.Payload.ExpirationDate,
            ["OptionType"] = envelope.Payload.OptionType ?? string.Empty,
            ["Exchange"] = envelope.Payload.Exchange ?? string.Empty,
            ["ImpliedVolatility"] = envelope.Payload.ImpliedVolatility,
            ["ContractSize"] = envelope.Payload.ContractSize ?? string.Empty,
            ["SettlementType"] = envelope.Payload.SettlementType ?? string.Empty,
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
            "OPTION",
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