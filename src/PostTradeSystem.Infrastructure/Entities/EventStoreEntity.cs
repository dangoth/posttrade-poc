namespace PostTradeSystem.Infrastructure.Entities;

public class EventStoreEntity
{
    public long Id { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public string PartitionKey { get; set; } = string.Empty;
    public long AggregateVersion { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EventData { get; set; } = string.Empty;
    public string Metadata { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string CausedBy { get; set; } = string.Empty;
    public bool IsProcessed { get; set; }
    public DateTime? ProcessedAt { get; set; }
}