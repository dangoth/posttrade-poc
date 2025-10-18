using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Messages;

namespace PostTradeSystem.Core.Adapters;

public interface ITradeMessageAdapterFactory
{
    Task<TradeCreatedEvent?> ProcessMessageAsync(string messageType, string sourceSystem, string messageValue, string correlationId);
    bool CanProcessMessage(string messageType, string sourceSystem);
}