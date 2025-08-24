namespace PostTradeSystem.Core.Messages;

public class TradeMessageEnvelope<T> where T : TradeMessage
{
    public string MessageId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public T Payload { get; set; } = default!;
    public Dictionary<string, string> Headers { get; set; } = new();
}