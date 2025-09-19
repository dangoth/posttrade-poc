using PostTradeSystem.Core.Events;

namespace PostTradeSystem.Infrastructure.Tests.TestHelpers;

public class TestDomainEvent : DomainEventBase
{
    public TestDomainEvent(string aggregateId, string aggregateType, long aggregateVersion, string correlationId, string causedBy, string? eventId = null)
        : base(aggregateId, aggregateType, aggregateVersion, correlationId, causedBy)
    {
        if (eventId != null)
        {
            typeof(DomainEventBase).GetProperty("EventId")!.SetValue(this, eventId);
        }
    }
}