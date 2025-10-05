namespace PostTradeSystem.Infrastructure.Entities;

public class OutboxEventEntity
{
    public long Id { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EventData { get; set; } = string.Empty;
    public string Metadata { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string PartitionKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public bool IsProcessed { get; set; }
    public int RetryCount { get; set; }
    public DateTime? LastRetryAt { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsDeadLettered { get; set; }
    public DateTime? DeadLetteredAt { get; set; }
    public string? DeadLetterReason { get; set; }
}