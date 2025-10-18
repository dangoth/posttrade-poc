using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Messages;

namespace PostTradeSystem.Core.Adapters;

public interface ITradeMessageAdapter<T> where T : TradeMessage
{
    string SourceSystem { get; }
    string MessageType { get; }
    Task<TradeCreatedEvent?> AdaptToEventAsync(TradeMessageEnvelope<T> envelope, string correlationId);
    bool CanHandle(string sourceSystem, string messageType);
}