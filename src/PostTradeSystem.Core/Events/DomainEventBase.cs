namespace PostTradeSystem.Core.Events;

public abstract class DomainEventBase : IDomainEvent
{
    protected DomainEventBase(string aggregateId, string aggregateType, long aggregateVersion, string correlationId, string causedBy)
    {
        EventId = Guid.NewGuid().ToString();
        AggregateId = aggregateId;
        AggregateType = aggregateType;
        OccurredAt = DateTime.UtcNow;
        AggregateVersion = aggregateVersion;
        CorrelationId = correlationId;
        CausedBy = causedBy;
    }

    public string EventId { get; private set; }
    public string AggregateId { get; private set; }
    public string AggregateType { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public long AggregateVersion { get; private set; }
    public string CorrelationId { get; private set; }
    public string CausedBy { get; private set; }
}