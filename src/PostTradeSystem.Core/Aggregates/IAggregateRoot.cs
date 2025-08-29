using PostTradeSystem.Core.Events;

namespace PostTradeSystem.Core.Aggregates;

public interface IAggregateRoot
{
    string Id { get; }
    string PartitionKey { get; }
    IReadOnlyList<IDomainEvent> GetUncommittedEvents();
    void MarkEventsAsCommitted();
    void LoadFromHistory(IEnumerable<IDomainEvent> events);
}