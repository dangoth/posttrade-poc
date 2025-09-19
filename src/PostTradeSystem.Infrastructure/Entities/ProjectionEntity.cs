namespace PostTradeSystem.Infrastructure.Entities;

public class ProjectionEntity
{
    public long Id { get; set; }
    public string ProjectionName { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public string PartitionKey { get; set; } = string.Empty;
    public long LastProcessedVersion { get; set; }
    public string ProjectionData { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}