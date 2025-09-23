using PostTradeSystem.Core.Events;

namespace PostTradeSystem.Core.Serialization.Contracts;

public class TradeStatusChangedEventV1 : IVersionedEventContract
{
    public int SchemaVersion => 1;
    public string EventType => "TradeStatusChanged";

    public string EventId { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public long AggregateVersion { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string CausedBy { get; set; } = string.Empty;
    
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class TradeStatusChangedEventV2 : IVersionedEventContract
{
    public int SchemaVersion => 2;
    public string EventType => "TradeStatusChanged";

    public string EventId { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public long AggregateVersion { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string CausedBy { get; set; } = string.Empty;
    
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime ApprovalTimestamp { get; set; }
    public string AuditTrail { get; set; } = string.Empty;
}

public class TradeStatusChangedEventV1ToV2Converter : IEventConverter<TradeStatusChangedEventV1, TradeStatusChangedEventV2>
{
    public TradeStatusChangedEventV2 Convert(TradeStatusChangedEventV1 source)
    {
        return new TradeStatusChangedEventV2
        {
            EventId = source.EventId,
            AggregateId = source.AggregateId,
            AggregateType = source.AggregateType,
            OccurredAt = source.OccurredAt,
            AggregateVersion = source.AggregateVersion,
            CorrelationId = source.CorrelationId,
            CausedBy = source.CausedBy,
            PreviousStatus = source.PreviousStatus,
            NewStatus = source.NewStatus,
            Reason = source.Reason,
            
            ApprovedBy = source.CausedBy,
            ApprovalTimestamp = source.OccurredAt,
            AuditTrail = $"Status changed from {source.PreviousStatus} to {source.NewStatus}. Reason: {source.Reason}"
        };
    }

    public bool CanConvert(int fromVersion, int toVersion)
    {
        return fromVersion == 1 && toVersion == 2;
    }
}

public class TradeStatusChangedEventV2ToV1Converter : IEventConverter<TradeStatusChangedEventV2, TradeStatusChangedEventV1>
{
    public TradeStatusChangedEventV1 Convert(TradeStatusChangedEventV2 source)
    {
        return new TradeStatusChangedEventV1
        {
            EventId = source.EventId,
            AggregateId = source.AggregateId,
            AggregateType = source.AggregateType,
            OccurredAt = source.OccurredAt,
            AggregateVersion = source.AggregateVersion,
            CorrelationId = source.CorrelationId,
            CausedBy = source.CausedBy,
            PreviousStatus = source.PreviousStatus,
            NewStatus = source.NewStatus,
            Reason = source.Reason
        };
    }

    public bool CanConvert(int fromVersion, int toVersion)
    {
        return fromVersion == 2 && toVersion == 1;
    }
}