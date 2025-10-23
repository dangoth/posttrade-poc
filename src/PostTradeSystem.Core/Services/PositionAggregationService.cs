using Microsoft.Extensions.Logging;
using PostTradeSystem.Core.Common;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Schemas;
using System.Text.Json;

namespace PostTradeSystem.Core.Services;

public class PositionAggregationService : IPositionAggregationService
{
    private readonly IJsonSchemaValidator _schemaValidator;
    private readonly ILogger<PositionAggregationService> _logger;

    public PositionAggregationService(
        IJsonSchemaValidator schemaValidator,
        ILogger<PositionAggregationService> logger)
    {
        _schemaValidator = schemaValidator;
        _logger = logger;
    }


    public Task<Result<PositionSummary>> CalculatePositionFromEventsAsync(IEnumerable<IDomainEvent> events, string traderId, string instrumentId)
    {
        try
        {
            var tradeEvents = events.OfType<TradeCreatedEvent>()
                .Where(e => e.TraderId == traderId && e.InstrumentId == instrumentId)
                .OrderBy(e => e.TradeDateTime)
                .ToList();

            if (!tradeEvents.Any())
            {
                return Task.FromResult(Result<PositionSummary>.Success(new PositionSummary
                {
                    TraderId = traderId,
                    InstrumentId = instrumentId,
                    NetQuantity = 0,
                    AveragePrice = 0,
                    TotalNotional = 0,
                    Currency = string.Empty,
                    LastUpdated = DateTime.UtcNow,
                    TradeCount = 0,
                    RealizedPnL = 0
                }));
            }

            decimal totalBuyQuantity = 0;
            decimal totalBuyNotional = 0;
            decimal totalSellQuantity = 0;
            decimal totalSellNotional = 0;
            var currency = tradeEvents.First().Currency;
            var lastUpdated = tradeEvents.Max(e => e.TradeDateTime);

            foreach (var trade in tradeEvents)
            {
                var notional = trade.Quantity * trade.Price;
                
                if (trade.Direction.Equals("Buy", StringComparison.OrdinalIgnoreCase))
                {
                    totalBuyQuantity += trade.Quantity;
                    totalBuyNotional += notional;
                }
                else if (trade.Direction.Equals("Sell", StringComparison.OrdinalIgnoreCase))
                {
                    totalSellQuantity += trade.Quantity;
                    totalSellNotional += notional;
                }
            }

            var netQuantity = totalBuyQuantity - totalSellQuantity;
            var netNotional = netQuantity == 0 ? 0 : totalBuyNotional - totalSellNotional;
            var averagePrice = netQuantity != 0 ? Math.Abs(netNotional / netQuantity) : 0;

            var realizedPnL = CalculateRealizedPnL(tradeEvents);

            var position = new PositionSummary
            {
                TraderId = traderId,
                InstrumentId = instrumentId,
                NetQuantity = netQuantity,
                AveragePrice = averagePrice,
                TotalNotional = netNotional,
                Currency = currency,
                LastUpdated = lastUpdated,
                TradeCount = tradeEvents.Count,
                RealizedPnL = realizedPnL,
                AdditionalMetrics = new Dictionary<string, object>
                {
                    ["TotalBuyQuantity"] = totalBuyQuantity,
                    ["TotalSellQuantity"] = totalSellQuantity,
                    ["TotalBuyNotional"] = totalBuyNotional,
                    ["TotalSellNotional"] = totalSellNotional,
                    ["IsLongPosition"] = netQuantity > 0,
                    ["IsShortPosition"] = netQuantity < 0,
                    ["IsFlatPosition"] = netQuantity == 0
                }
            };

            var validationResult = ValidatePositionSummary(position);
            if (validationResult.IsFailure)
            {
                return Task.FromResult(Result<PositionSummary>.Failure($"Position summary validation failed: {validationResult.Error}"));
            }

            return Task.FromResult(Result<PositionSummary>.Success(position));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating position from events for trader {TraderId}, instrument {InstrumentId}", traderId, instrumentId);
            return Task.FromResult(Result<PositionSummary>.Failure($"Error calculating position from events: {ex.Message}"));
        }
    }


    private static decimal CalculateRealizedPnL(List<TradeCreatedEvent> trades)
    {
        var realizedPnL = 0m;
        var position = 0m;
        var averageCost = 0m;

        foreach (var trade in trades.OrderBy(t => t.TradeDateTime))
        {
            var quantity = trade.Direction.Equals("Buy", StringComparison.OrdinalIgnoreCase) ? trade.Quantity : -trade.Quantity;
            
            if (position == 0)
            {
                position = quantity;
                averageCost = trade.Price;
            }
            else if (Math.Sign(position) == Math.Sign(quantity))
            {
                var totalNotional = (position * averageCost) + (quantity * trade.Price);
                position += quantity;
                averageCost = position != 0 ? totalNotional / position : 0;
            }
            else
            {
                var closingQuantity = Math.Min(Math.Abs(quantity), Math.Abs(position));
                realizedPnL += closingQuantity * (trade.Price - averageCost) * Math.Sign(position);
                
                position += quantity;
                if (position == 0)
                {
                    averageCost = 0;
                }
            }
        }

        return realizedPnL;
    }

    private Result ValidatePositionSummary(PositionSummary position)
    {
        var positionData = new
        {
            TraderId = position.TraderId,
            InstrumentId = position.InstrumentId,
            NetQuantity = position.NetQuantity,
            AveragePrice = position.AveragePrice,
            TotalNotional = position.TotalNotional,
            Currency = position.Currency,
            LastUpdated = position.LastUpdated,
            TradeCount = position.TradeCount,
            RealizedPnL = position.RealizedPnL,
            AdditionalMetrics = position.AdditionalMetrics
        };

        var jsonMessage = JsonSerializer.Serialize(positionData);
        var isValid = _schemaValidator.ValidateMessage("PositionSummary", jsonMessage, null);
        if (!isValid)
        {
            return Result.Failure("Schema validation failed for PositionSummary");
        }

        return Result.Success();
    }
}