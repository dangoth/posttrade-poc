using PostTradeSystem.Core.Messages;
using PostTradeSystem.Api.Services;

namespace PostTradeSystem.Api.Endpoints;

public static class TradeEndpoints
{
    // Option 1: Using a Service Class (Recommended)
    public static void MapTradeEndpoints(this WebApplication app)
    {
        app.MapPost("/trades/equity", async (EquityTradeMessage trade, TradeService tradeService) =>
        {
            try
            {
                var result = await tradeService.ProduceEquityTradeAsync(trade);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Success = false, Error = ex.Message });
            }
        })
        .WithName("ProduceEquityTrade")
        .WithOpenApi();

        app.MapPost("/trades/option", async (OptionTradeMessage trade, TradeService tradeService) =>
        {
            try
            {
                var result = await tradeService.ProduceOptionTradeAsync(trade);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Success = false, Error = ex.Message });
            }
        })
        .WithName("ProduceOptionTrade")
        .WithOpenApi();

        app.MapPost("/trades/fx", async (FxTradeMessage trade, TradeService tradeService) =>
        {
            try
            {
                var result = await tradeService.ProduceFxTradeAsync(trade);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Success = false, Error = ex.Message });
            }
        })
        .WithName("ProduceFxTrade")
        .WithOpenApi();
    }
}