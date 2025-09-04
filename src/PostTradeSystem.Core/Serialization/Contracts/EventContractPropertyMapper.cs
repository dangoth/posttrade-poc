using PostTradeSystem.Core.Events;

namespace PostTradeSystem.Core.Serialization.Contracts;

public static class EventContractPropertyMapper
{
    // Maps base domain event properties to event contract
    public static void MapBaseProperties(IDomainEvent domainEvent, BaseEventContract contract)
    {
        contract.EventId = domainEvent.EventId;
        contract.AggregateId = domainEvent.AggregateId;
        contract.AggregateType = domainEvent.AggregateType;
        contract.OccurredAt = domainEvent.OccurredAt;
        contract.AggregateVersion = domainEvent.AggregateVersion;
        contract.CorrelationId = domainEvent.CorrelationId;
        contract.CausedBy = domainEvent.CausedBy;
    }

    // Maps base properties from one contract to another (for version conversion)
    public static void MapBaseProperties(BaseEventContract source, BaseEventContract target)
    {
        target.EventId = source.EventId;
        target.AggregateId = source.AggregateId;
        target.AggregateType = source.AggregateType;
        target.OccurredAt = source.OccurredAt;
        target.AggregateVersion = source.AggregateVersion;
        target.CorrelationId = source.CorrelationId;
        target.CausedBy = source.CausedBy;
    }
}