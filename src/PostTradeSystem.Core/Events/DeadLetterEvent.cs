namespace PostTradeSystem.Core.Events;

public class DeadLetterEvent : DomainEventBase
{
    public string OriginalEventType { get; }
    public string OriginalEventData { get; }
    public string OriginalMetadata { get; }
    public string DeadLetterReason { get; }

    public DeadLetterEvent(
        string aggregateId,
        string originalEventType,
        string originalEventData,
        string originalMetadata,
        string deadLetterReason,
        long aggregateVersion,
        string correlationId,
        string causedBy) 
        : base(aggregateId, "DeadLetter", aggregateVersion, correlationId, causedBy)
    {
        OriginalEventType = originalEventType;
        OriginalEventData = originalEventData;
        OriginalMetadata = originalMetadata;
        DeadLetterReason = deadLetterReason;
    }
}