namespace PostTradeSystem.Core.Events;

public interface IDomainEvent
{
    string EventId { get; }
    string AggregateId { get; }
    string AggregateType { get; }
    DateTime OccurredAt { get; }
    long AggregateVersion { get; }
    string CorrelationId { get; }
    string CausedBy { get; }
}