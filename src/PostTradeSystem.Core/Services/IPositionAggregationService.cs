using PostTradeSystem.Core.Common;
using PostTradeSystem.Core.Events;

namespace PostTradeSystem.Core.Services;

public interface IPositionAggregationService
{
    Task<Result<PositionSummary>> CalculatePositionFromEventsAsync(IEnumerable<IDomainEvent> events, string traderId, string instrumentId);
}

public class PositionSummary
{
    public string TraderId { get; set; } = string.Empty;
    public string InstrumentId { get; set; } = string.Empty;
    public decimal NetQuantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal TotalNotional { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public int TradeCount { get; set; }
    public decimal RealizedPnL { get; set; }
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}