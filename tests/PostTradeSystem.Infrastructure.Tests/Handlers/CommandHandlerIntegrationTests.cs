using Xunit;
using PostTradeSystem.Core.Handlers;
using PostTradeSystem.Core.Commands;
using PostTradeSystem.Core.Aggregates;
using PostTradeSystem.Core.Models;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using PostTradeSystem.Infrastructure.Tests.Integration;

namespace PostTradeSystem.Infrastructure.Tests.Handlers;

[Collection("SqlServer")]
public class CommandHandlerIntegrationTests : IntegrationTestBase
{
    private readonly ICommandHandler<CreateTradeCommand> _createHandler;
    private readonly ICommandHandler<UpdateTradeStatusCommand> _updateHandler;
    private readonly ICommandHandler<EnrichTradeCommand> _enrichHandler;
    private readonly ICommandHandler<ValidateTradeCommand> _validateHandler;
    private readonly IAggregateRepository<TradeAggregate> _repository;

    public CommandHandlerIntegrationTests(SqlServerFixture fixture) : base(fixture)
    {
        _createHandler = GetRequiredService<ICommandHandler<CreateTradeCommand>>();
        _updateHandler = GetRequiredService<ICommandHandler<UpdateTradeStatusCommand>>();
        _enrichHandler = GetRequiredService<ICommandHandler<EnrichTradeCommand>>();
        _validateHandler = GetRequiredService<ICommandHandler<ValidateTradeCommand>>();
        _repository = GetRequiredService<IAggregateRepository<TradeAggregate>>();
    }

    [Fact]
    public async Task CreateTradeCommandHandler_EndToEnd_CreatesAndPersistsTrade()
    {
        var command = new CreateTradeCommand(
            "INTEGRATION-TRADE-001",
            "TRADER-001",
            "INSTRUMENT-001",
            100,
            50.0m,
            "Buy",
            DateTime.UtcNow.AddMinutes(-1),
            "USD",
            "COUNTERPARTY-001",
            "Equity",
            new Dictionary<string, object> { { "Source", "IntegrationTest" } },
            "CORR-001",
            "TestUser");

        var result = await _createHandler.HandleAsync(command);

        Assert.True(result.IsSuccess);

        var retrievedTradeResult = await _repository.GetByIdAsync(command.AggregateId);
        Assert.True(retrievedTradeResult.IsSuccess);
        Assert.NotNull(retrievedTradeResult.Value);

        var trade = retrievedTradeResult.Value!;
        Assert.Equal(command.AggregateId, trade.Id);
        Assert.Equal(command.TraderId, trade.TraderId);
        Assert.Equal(command.InstrumentId, trade.InstrumentId);
        Assert.Equal(command.Quantity, trade.Quantity);
        Assert.Equal(command.Price, trade.Price);
        Assert.Equal(TradeDirection.Buy, trade.Direction);
        Assert.Equal(command.Currency, trade.Currency);
        Assert.Equal(TradeStatus.Pending, trade.Status);
    }

    [Fact]
    public async Task UpdateTradeStatusCommandHandler_EndToEnd_UpdatesTradeStatus()
    {
        var tradeId = "INTEGRATION-TRADE-002";
        
        var createCommand = new CreateTradeCommand(
            tradeId, "TRADER-002", "INSTRUMENT-002", 200, 75.0m, "Sell",
            DateTime.UtcNow.AddMinutes(-2), "EUR", "COUNTERPARTY-002", "Equity",
            new Dictionary<string, object>(), "CORR-002", "TestUser");

        await _createHandler.HandleAsync(createCommand);

        var updateCommand = new UpdateTradeStatusCommand(
            tradeId, "Executed", "Manual execution", "CORR-003", "TestUser");

        var result = await _updateHandler.HandleAsync(updateCommand);

        Assert.True(result.IsSuccess);

        var retrievedTradeResult = await _repository.GetByIdAsync(tradeId);
        Assert.True(retrievedTradeResult.IsSuccess);
        Assert.NotNull(retrievedTradeResult.Value);
        Assert.Equal(TradeStatus.Executed, retrievedTradeResult.Value!.Status);
    }

    [Fact]
    public async Task EnrichTradeCommandHandler_EndToEnd_EnrichesTrade()
    {
        var tradeId = "INTEGRATION-TRADE-003";
        
        var createCommand = new CreateTradeCommand(
            tradeId, "TRADER-003", "INSTRUMENT-003", 300, 100.0m, "Buy",
            DateTime.UtcNow.AddMinutes(-3), "GBP", "COUNTERPARTY-003", "Equity",
            new Dictionary<string, object>(), "CORR-004", "TestUser");

        await _createHandler.HandleAsync(createCommand);

        var enrichCommand = new EnrichTradeCommand(
            tradeId, "RiskData", 
            new Dictionary<string, object> 
            { 
                { "RiskScore", 85 }, 
                { "VaR", 1250.50d } 
            },
            "CORR-005", "RiskSystem");

        var result = await _enrichHandler.HandleAsync(enrichCommand);

        Assert.True(result.IsSuccess);

        var retrievedTradeResult = await _repository.GetByIdAsync(tradeId);
        Assert.True(retrievedTradeResult.IsSuccess);
        Assert.NotNull(retrievedTradeResult.Value);
        
        var trade = retrievedTradeResult.Value!;
        Assert.True(trade.AdditionalData.ContainsKey("RiskData_RiskScore"));
        Assert.True(trade.AdditionalData.ContainsKey("RiskData_VaR"));
        Assert.Equal(85, trade.AdditionalData["RiskData_RiskScore"]);
        Assert.Equal(1250.50d, (double)trade.AdditionalData["RiskData_VaR"], precision: 2);
    }

    [Fact]
    public async Task ValidateTradeCommandHandler_EndToEnd_ValidatesTradeAndRecordsFailures()
    {
        var tradeId = "INTEGRATION-TRADE-004";
        
        var createCommand = new CreateTradeCommand(
            tradeId, "TRADER-004", "INSTRUMENT-004", 400, 125.0m, "Buy",
            DateTime.UtcNow.AddMinutes(10), "INVALID", "COUNTERPARTY-004", "Equity",
            new Dictionary<string, object>(), "CORR-006", "TestUser");

        await _createHandler.HandleAsync(createCommand);

        var validateCommand = new ValidateTradeCommand(tradeId, "CORR-007", "ValidationSystem");

        var result = await _validateHandler.HandleAsync(validateCommand);

        Assert.True(result.IsFailure);
        Assert.Contains("validation failed", result.Error);

        var retrievedTradeResult = await _repository.GetByIdAsync(tradeId);
        Assert.True(retrievedTradeResult.IsSuccess);
        Assert.NotNull(retrievedTradeResult.Value);
        
        var trade = retrievedTradeResult.Value!;
        Assert.Equal(TradeStatus.Failed, trade.Status);
        Assert.NotEmpty(trade.ValidationErrors);
        Assert.Contains(trade.ValidationErrors, e => e.Contains("future"));
        Assert.Contains(trade.ValidationErrors, e => e.Contains("Currency"));
    }

    [Fact]
    public async Task CommandHandlers_WithIdempotency_HandlesRepeatedCommands()
    {

        var command = new CreateTradeCommand(
            "INTEGRATION-TRADE-005",
            "TRADER-005",
            "INSTRUMENT-005",
            500,
            150.0m,
            "Sell",
            DateTime.UtcNow.AddMinutes(-5),
            "JPY",
            "COUNTERPARTY-005",
            "Equity",
            new Dictionary<string, object>(),
            "CORR-008",
            "TestUser");

        var result1 = await _createHandler.HandleAsync(command);
        var result2 = await _createHandler.HandleAsync(command);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsFailure);
        Assert.Contains("already exists", result2.Error);

        var retrievedTradeResult = await _repository.GetByIdAsync(command.AggregateId);
        Assert.True(retrievedTradeResult.IsSuccess);
        Assert.NotNull(retrievedTradeResult.Value);
    }
}