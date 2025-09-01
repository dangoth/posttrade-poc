using PostTradeSystem.Core.Events;

namespace PostTradeSystem.Core.Aggregates;

public abstract class AggregateRootBase : IAggregateRoot
{
    private readonly List<IDomainEvent> _uncommittedEvents = new();
    protected long _eventSequence = 0;

    protected AggregateRootBase(string id, string partitionKey)
    {
        Id = id;
        PartitionKey = partitionKey;
    }

    public string Id { get; private set; }
    public string PartitionKey { get; private set; }

    public IReadOnlyList<IDomainEvent> GetUncommittedEvents()
    {
        return _uncommittedEvents.AsReadOnly();
    }

    public void MarkEventsAsCommitted()
    {
        _uncommittedEvents.Clear();
    }

    public void LoadFromHistory(IEnumerable<IDomainEvent> events)
    {
        foreach (var domainEvent in events.OrderBy(e => e.AggregateVersion))
        {
            ApplyEvent(domainEvent, false);
            _eventSequence = domainEvent.AggregateVersion;
        }
    }

    protected void ApplyEvent(IDomainEvent domainEvent, bool isNew = true)
    {
        ApplyEventToState(domainEvent);

        if (isNew)
        {
            _eventSequence++;
            _uncommittedEvents.Add(domainEvent);
        }
    }

    protected abstract void ApplyEventToState(IDomainEvent domainEvent);

    protected T CreateEvent<T>(Func<long, string, string, T> eventFactory, string correlationId, string causedBy) 
        where T : IDomainEvent
    {
        return eventFactory(_eventSequence + 1, correlationId, causedBy);
    }
}