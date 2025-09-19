namespace PostTradeSystem.Infrastructure.Entities;

public class IdempotencyEntity
{
    public long Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ResponseData { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}