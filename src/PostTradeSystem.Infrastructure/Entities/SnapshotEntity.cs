namespace PostTradeSystem.Infrastructure.Entities;

public class SnapshotEntity
{
    public long Id { get; set; }
    public string AggregateId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public string PartitionKey { get; set; } = string.Empty;
    public long AggregateVersion { get; set; }
    public string SnapshotData { get; set; } = string.Empty;
    public string Metadata { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}