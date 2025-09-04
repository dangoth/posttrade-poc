using PostTradeSystem.Core.Messages;
using PostTradeSystem.Api.Services;

namespace PostTradeSystem.Api.Endpoints;

public static class TradeEndpoints
{
    public static void MapTradeEndpoints(this WebApplication app)
    {
        app.MapPost("/trades/equity", async (EquityTradeMessage trade, TradeService tradeService) =>
            await HandleTradeEndpoint(() => tradeService.ProduceEquityTradeAsync(trade)))
        .WithName("ProduceEquityTrade")
        .WithOpenApi();

        app.MapPost("/trades/option", async (OptionTradeMessage trade, TradeService tradeService) =>
            await HandleTradeEndpoint(() => tradeService.ProduceOptionTradeAsync(trade)))
        .WithName("ProduceOptionTrade")
        .WithOpenApi();

        app.MapPost("/trades/fx", async (FxTradeMessage trade, TradeService tradeService) =>
            await HandleTradeEndpoint(() => tradeService.ProduceFxTradeAsync(trade)))
        .WithName("ProduceFxTrade")
        .WithOpenApi();
    }

    private static async Task<IResult> HandleTradeEndpoint(Func<Task<object>> tradeOperation)
    {
        try
        {
            var result = await tradeOperation();
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { Success = false, Error = ex.Message });
        }
    }
}