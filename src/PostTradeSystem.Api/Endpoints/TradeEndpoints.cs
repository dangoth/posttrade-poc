using PostTradeSystem.Core.Messages;
using PostTradeSystem.Api.Services;

namespace PostTradeSystem.Api.Endpoints;

public static class TradeEndpoints
{
    public static void MapTradeEndpoints(this WebApplication app)
    {
        app.MapPost("/trades/equity", async (EquityTradeMessage trade, TradeService tradeService) =>
        {
            var result = await tradeService.ProduceEquityTradeAsync(trade);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { Success = false, Error = result.Error });
        })
        .WithName("ProduceEquityTrade")
        .WithOpenApi();

        app.MapPost("/trades/option", async (OptionTradeMessage trade, TradeService tradeService) =>
        {
            var result = await tradeService.ProduceOptionTradeAsync(trade);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { Success = false, Error = result.Error });
        })
        .WithName("ProduceOptionTrade")
        .WithOpenApi();

        app.MapPost("/trades/fx", async (FxTradeMessage trade, TradeService tradeService) =>
        {
            var result = await tradeService.ProduceFxTradeAsync(trade);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { Success = false, Error = result.Error });
        })
        .WithName("ProduceFxTrade")
        .WithOpenApi();
    }
}