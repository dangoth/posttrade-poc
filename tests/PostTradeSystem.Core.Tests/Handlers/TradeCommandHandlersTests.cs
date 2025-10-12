using Xunit;
using Moq;
using PostTradeSystem.Core.Handlers;
using PostTradeSystem.Core.Commands;
using PostTradeSystem.Core.Aggregates;
using PostTradeSystem.Core.Common;
using PostTradeSystem.Core.Models;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Handlers;

namespace PostTradeSystem.Core.Tests.Handlers;

public class CreateTradeCommandHandlerTests
{
    private readonly Mock<IAggregateRepository<TradeAggregate>> _mockRepository;
    private readonly Mock<IJsonSchemaValidator> _mockSchemaValidator;
    private readonly CreateTradeCommandHandler _handler;

    public CreateTradeCommandHandlerTests()
    {
        _mockRepository = new Mock<IAggregateRepository<TradeAggregate>>();
        _mockSchemaValidator = new Mock<IJsonSchemaValidator>();
        _mockSchemaValidator.Setup(v => v.ValidateMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
            .Returns(true);
        _handler = new CreateTradeCommandHandler(_mockRepository.Object, _mockSchemaValidator.Object);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_CreatesTradeSuccessfully()
    {
        var command = CreateValidCommand();
        _mockRepository.Setup(r => r.GetByIdAsync(command.AggregateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TradeAggregate?>.Success(null));
        _mockRepository.Setup(r => r.SaveAsync(It.IsAny<TradeAggregate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<TradeAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TradeAlreadyExists_ReturnsFailure()
    {
        var command = CreateValidCommand();
        var existingTrade = TradeAggregate.CreateTrade(
            command.AggregateId, command.TraderId, command.InstrumentId,
            command.Quantity, command.Price, TradeDirection.Buy,
            command.TradeDateTime, command.Currency, command.CounterpartyId,
            command.TradeType, command.AdditionalData, command.CorrelationId, command.CausedBy);

        _mockRepository.Setup(r => r.GetByIdAsync(command.AggregateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TradeAggregate?>.Success(existingTrade));

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists", result.Error);
        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<TradeAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("", "TraderId", "InstrumentId", 100, 50.0, "Buy", "USD", "CounterpartyId", "Equity", "Trade ID is required")]
    [InlineData("TradeId", "", "InstrumentId", 100, 50.0, "Buy", "USD", "CounterpartyId", "Equity", "Trader ID is required")]
    [InlineData("TradeId", "TraderId", "", 100, 50.0, "Buy", "USD", "CounterpartyId", "Equity", "Instrument ID is required")]
    [InlineData("TradeId", "TraderId", "InstrumentId", 0, 50.0, "Buy", "USD", "CounterpartyId", "Equity", "Quantity must be greater than zero")]
    [InlineData("TradeId", "TraderId", "InstrumentId", 100, 0, "Buy", "USD", "CounterpartyId", "Equity", "Price must be greater than zero")]
    [InlineData("TradeId", "TraderId", "InstrumentId", 100, 50.0, "", "USD", "CounterpartyId", "Equity", "Direction is required")]
    [InlineData("TradeId", "TraderId", "InstrumentId", 100, 50.0, "Buy", "", "CounterpartyId", "Equity", "Currency is required")]
    [InlineData("TradeId", "TraderId", "InstrumentId", 100, 50.0, "Buy", "USD", "", "Equity", "Counterparty ID is required")]
    [InlineData("TradeId", "TraderId", "InstrumentId", 100, 50.0, "Buy", "USD", "CounterpartyId", "", "Trade type is required")]
    public async Task HandleAsync_InvalidCommand_ReturnsValidationFailure(
        string tradeId, string traderId, string instrumentId, decimal quantity, decimal price,
        string direction, string currency, string counterpartyId, string tradeType, string expectedError)
    {
        var command = new CreateTradeCommand(
            tradeId, traderId, instrumentId, quantity, price, direction,
            DateTime.UtcNow, currency, counterpartyId, tradeType,
            new Dictionary<string, object>(), "correlationId", "causedBy");

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains(expectedError, result.Error);
    }

    [Fact]
    public async Task HandleAsync_InvalidDirection_ReturnsFailure()
    {
        var command = CreateValidCommand();
        command = new CreateTradeCommand(
            command.AggregateId, command.TraderId, command.InstrumentId,
            command.Quantity, command.Price, "InvalidDirection",
            command.TradeDateTime, command.Currency, command.CounterpartyId,
            command.TradeType, command.AdditionalData, command.CorrelationId, command.CausedBy);

        _mockRepository.Setup(r => r.GetByIdAsync(command.AggregateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TradeAggregate?>.Success(null));

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Invalid trade direction", result.Error);
    }

    private static CreateTradeCommand CreateValidCommand()
    {
        return new CreateTradeCommand(
            "TRADE-001", "TRADER-001", "INSTRUMENT-001",
            100, 50.0m, "Buy", DateTime.UtcNow.AddMinutes(-1),
            "USD", "COUNTERPARTY-001", "Equity",
            new Dictionary<string, object> { { "Source", "TestSystem" } },
            "CORR-001", "TestUser");
    }
}

public class UpdateTradeStatusCommandHandlerTests
{
    private readonly Mock<IAggregateRepository<TradeAggregate>> _mockRepository;
    private readonly Mock<IJsonSchemaValidator> _mockSchemaValidator;
    private readonly UpdateTradeStatusCommandHandler _handler;

    public UpdateTradeStatusCommandHandlerTests()
    {
        _mockRepository = new Mock<IAggregateRepository<TradeAggregate>>();
        _mockSchemaValidator = new Mock<IJsonSchemaValidator>();
        _mockSchemaValidator.Setup(v => v.ValidateMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
            .Returns(true);
        _handler = new UpdateTradeStatusCommandHandler(_mockRepository.Object, _mockSchemaValidator.Object);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_UpdatesStatusSuccessfully()
    {
        var command = new UpdateTradeStatusCommand("TRADE-001", "Executed", "Manual execution", "CORR-001", "TestUser");
        var trade = CreateTestTrade();

        _mockRepository.Setup(r => r.GetByIdAsync(command.AggregateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TradeAggregate?>.Success(trade));
        _mockRepository.Setup(r => r.SaveAsync(It.IsAny<TradeAggregate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<TradeAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TradeNotFound_ReturnsFailure()
    {
        var command = new UpdateTradeStatusCommand("TRADE-001", "Executed", "Manual execution", "CORR-001", "TestUser");

        _mockRepository.Setup(r => r.GetByIdAsync(command.AggregateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TradeAggregate?>.Success(null));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error);
    }

    [Theory]
    [InlineData("", "Executed", "Reason", "Trade ID is required")]
    [InlineData("TRADE-001", "", "Reason", "New status is required")]
    [InlineData("TRADE-001", "Executed", "", "Reason is required")]
    public async Task HandleAsync_InvalidCommand_ReturnsValidationFailure(
        string tradeId, string newStatus, string reason, string expectedError)
    {
        var command = new UpdateTradeStatusCommand(tradeId, newStatus, reason, "CORR-001", "TestUser");

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains(expectedError, result.Error);
    }

    private static TradeAggregate CreateTestTrade()
    {
        return TradeAggregate.CreateTrade(
            "TRADE-001", "TRADER-001", "INSTRUMENT-001",
            100, 50.0m, TradeDirection.Buy, DateTime.UtcNow.AddMinutes(-1),
            "USD", "COUNTERPARTY-001", "Equity",
            new Dictionary<string, object>(), "CORR-001", "TestUser");
    }
}

public class EnrichTradeCommandHandlerTests
{
    private readonly Mock<IAggregateRepository<TradeAggregate>> _mockRepository;
    private readonly Mock<IJsonSchemaValidator> _mockSchemaValidator;
    private readonly EnrichTradeCommandHandler _handler;

    public EnrichTradeCommandHandlerTests()
    {
        _mockRepository = new Mock<IAggregateRepository<TradeAggregate>>();
        _mockSchemaValidator = new Mock<IJsonSchemaValidator>();
        _mockSchemaValidator.Setup(v => v.ValidateMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
            .Returns(true);
        _handler = new EnrichTradeCommandHandler(_mockRepository.Object, _mockSchemaValidator.Object);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_EnrichesTradeSuccessfully()
    {
        var command = new EnrichTradeCommand("TRADE-001", "RiskData", 
            new Dictionary<string, object> { { "RiskScore", 85 } }, "CORR-001", "TestUser");
        var trade = CreateTestTrade();

        _mockRepository.Setup(r => r.GetByIdAsync(command.AggregateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TradeAggregate?>.Success(trade));
        _mockRepository.Setup(r => r.SaveAsync(It.IsAny<TradeAggregate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<TradeAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TradeNotFound_ReturnsFailure()
    {
        var command = new EnrichTradeCommand("TRADE-001", "RiskData", 
            new Dictionary<string, object> { { "RiskScore", 85 } }, "CORR-001", "TestUser");

        _mockRepository.Setup(r => r.GetByIdAsync(command.AggregateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TradeAggregate?>.Success(null));

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error);
    }

    [Theory]
    [InlineData("", "RiskData", "Trade ID is required")]
    [InlineData("TRADE-001", "", "Enrichment type is required")]
    public async Task HandleAsync_InvalidCommand_ReturnsValidationFailure(
        string tradeId, string enrichmentType, string expectedError)
    {
        var command = new EnrichTradeCommand(tradeId, enrichmentType, 
            new Dictionary<string, object> { { "Key", "Value" } }, "CORR-001", "TestUser");

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains(expectedError, result.Error);
    }

    [Fact]
    public async Task HandleAsync_EmptyEnrichmentData_ReturnsValidationFailure()
    {
        var command = new EnrichTradeCommand("TRADE-001", "RiskData", 
            new Dictionary<string, object>(), "CORR-001", "TestUser");

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Enrichment data is required", result.Error);
    }

    private static TradeAggregate CreateTestTrade()
    {
        return TradeAggregate.CreateTrade(
            "TRADE-001", "TRADER-001", "INSTRUMENT-001",
            100, 50.0m, TradeDirection.Buy, DateTime.UtcNow.AddMinutes(-1),
            "USD", "COUNTERPARTY-001", "Equity",
            new Dictionary<string, object>(), "CORR-001", "TestUser");
    }
}

public class ValidateTradeCommandHandlerTests
{
    private readonly Mock<IAggregateRepository<TradeAggregate>> _mockRepository;
    private readonly Mock<IJsonSchemaValidator> _mockSchemaValidator;
    private readonly ValidateTradeCommandHandler _handler;

    public ValidateTradeCommandHandlerTests()
    {
        _mockRepository = new Mock<IAggregateRepository<TradeAggregate>>();
        _mockSchemaValidator = new Mock<IJsonSchemaValidator>();
        _mockSchemaValidator.Setup(v => v.ValidateMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
            .Returns(true);
        _handler = new ValidateTradeCommandHandler(_mockRepository.Object, _mockSchemaValidator.Object);
    }

    [Fact]
    public async Task HandleAsync_ValidTrade_ReturnsSuccess()
    {
        var command = new ValidateTradeCommand("TRADE-001", "CORR-001", "TestUser");
        var trade = CreateValidTrade();

        _mockRepository.Setup(r => r.GetByIdAsync(command.AggregateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TradeAggregate?>.Success(trade));

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task HandleAsync_InvalidTrade_RecordsValidationFailure()
    {
        var command = new ValidateTradeCommand("TRADE-001", "CORR-001", "TestUser");
        var trade = CreateInvalidTrade();

        _mockRepository.Setup(r => r.GetByIdAsync(command.AggregateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TradeAggregate?>.Success(trade));
        _mockRepository.Setup(r => r.SaveAsync(It.IsAny<TradeAggregate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains("validation failed", result.Error);
        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<TradeAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static TradeAggregate CreateValidTrade()
    {
        return TradeAggregate.CreateTrade(
            "TRADE-001", "TRADER-001", "INSTRUMENT-001",
            100, 50.0m, TradeDirection.Buy, DateTime.UtcNow.AddMinutes(-1),
            "USD", "COUNTERPARTY-001", "Equity",
            new Dictionary<string, object>(), "CORR-001", "TestUser");
    }

    private static TradeAggregate CreateInvalidTrade()
    {
        return TradeAggregate.CreateTrade(
            "TRADE-001", "TRADER-001", "INSTRUMENT-001",
            -100, -50.0m, TradeDirection.Buy, DateTime.UtcNow.AddDays(-40),
            "INVALID", "COUNTERPARTY-001", "Equity",
            new Dictionary<string, object>(), "CORR-001", "TestUser");
    }
}