using PostTradeSystem.Core.Commands;
using PostTradeSystem.Core.Common;
using PostTradeSystem.Core.Aggregates;
using PostTradeSystem.Core.Models;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Handlers;
using PostTradeSystem.Infrastructure.Repositories;

namespace PostTradeSystem.Infrastructure.Handlers;

public class CreateTradeCommandHandler : ICommandHandler<CreateTradeCommand>
{
    private readonly IAggregateRepository<TradeAggregate> _repository;
    private readonly IJsonSchemaValidator _schemaValidator;

    public CreateTradeCommandHandler(IAggregateRepository<TradeAggregate> repository, IJsonSchemaValidator schemaValidator)
    {
        _repository = repository;
        _schemaValidator = schemaValidator;
    }

    public async Task<Result> HandleAsync(CreateTradeCommand command, CancellationToken cancellationToken = default)
    {
        var schemaValidationResult = ValidateCommandSchema(command);
        if (schemaValidationResult.IsFailure)
            return schemaValidationResult;

        var validationResult = ValidateCommand(command);
        if (validationResult.IsFailure)
            return validationResult;

        var existingTradeResult = await _repository.GetByIdAsync(command.AggregateId, cancellationToken);
        if (existingTradeResult.IsFailure)
            return Result.Failure($"Failed to check existing trade: {existingTradeResult.Error}");

        if (existingTradeResult.Value != null)
            return Result.Failure($"Trade with ID {command.AggregateId} already exists");

        if (!Enum.TryParse<TradeDirection>(command.Direction, true, out var direction))
            return Result.Failure($"Invalid trade direction: {command.Direction}");

        var trade = TradeAggregate.CreateTrade(
            command.AggregateId,
            command.TraderId,
            command.InstrumentId,
            command.Quantity,
            command.Price,
            direction,
            command.TradeDateTime,
            command.Currency,
            command.CounterpartyId,
            command.TradeType,
            command.AdditionalData,
            command.CorrelationId,
            command.CausedBy);

        return await _repository.SaveAsync(trade, cancellationToken);
    }

    private Result ValidateCommandSchema(CreateTradeCommand command)
    {
        var commandData = new
        {
            TradeId = command.AggregateId,
            TraderId = command.TraderId,
            InstrumentId = command.InstrumentId,
            Quantity = command.Quantity,
            Price = command.Price,
            Direction = command.Direction,
            TradeDateTime = command.TradeDateTime,
            Currency = command.Currency,
            CounterpartyId = command.CounterpartyId,
            Status = "PENDING",
            SourceSystem = "PostTradeSystem",
            MessageType = command.TradeType
        };

        var jsonMessage = System.Text.Json.JsonSerializer.Serialize(commandData);
        var isValid = _schemaValidator.ValidateMessage("TradeMessage", jsonMessage);
        if (!isValid)
        {
            return Result.Failure("Schema validation failed for CreateTradeCommand");
        }

        return Result.Success();
    }

    private static Result ValidateCommand(CreateTradeCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.AggregateId))
            return Result.Failure("Trade ID is required");

        if (string.IsNullOrWhiteSpace(command.TraderId))
            return Result.Failure("Trader ID is required");

        if (string.IsNullOrWhiteSpace(command.InstrumentId))
            return Result.Failure("Instrument ID is required");

        if (command.Quantity <= 0)
            return Result.Failure("Quantity must be greater than zero");

        if (command.Price <= 0)
            return Result.Failure("Price must be greater than zero");

        if (string.IsNullOrWhiteSpace(command.Direction))
            return Result.Failure("Direction is required");

        if (string.IsNullOrWhiteSpace(command.Currency))
            return Result.Failure("Currency is required");

        if (string.IsNullOrWhiteSpace(command.CounterpartyId))
            return Result.Failure("Counterparty ID is required");

        if (string.IsNullOrWhiteSpace(command.TradeType))
            return Result.Failure("Trade type is required");

        if (command.TradeDateTime == default)
            return Result.Failure("Trade date time is required");

        return Result.Success();
    }
}

public class UpdateTradeStatusCommandHandler : ICommandHandler<UpdateTradeStatusCommand>
{
    private readonly IAggregateRepository<TradeAggregate> _repository;
    private readonly IJsonSchemaValidator _schemaValidator;

    public UpdateTradeStatusCommandHandler(IAggregateRepository<TradeAggregate> repository, IJsonSchemaValidator schemaValidator)
    {
        _repository = repository;
        _schemaValidator = schemaValidator;
    }

    public async Task<Result> HandleAsync(UpdateTradeStatusCommand command, CancellationToken cancellationToken = default)
    {
        var schemaValidationResult = ValidateCommandSchema(command);
        if (schemaValidationResult.IsFailure)
            return schemaValidationResult;

        var validationResult = ValidateCommand(command);
        if (validationResult.IsFailure)
            return validationResult;

        var tradeResult = await _repository.GetByIdAsync(command.AggregateId, cancellationToken);
        if (tradeResult.IsFailure)
            return Result.Failure($"Failed to load trade: {tradeResult.Error}");

        if (tradeResult.Value == null)
            return Result.Failure($"Trade with ID {command.AggregateId} not found");

        if (!Enum.TryParse<TradeStatus>(command.NewStatus, true, out var newStatus))
            return Result.Failure($"Invalid trade status: {command.NewStatus}");

        var trade = tradeResult.Value;
        var statusChangeResult = trade.ChangeStatus(newStatus, command.Reason, command.CorrelationId, command.CausedBy);
        if (statusChangeResult.IsFailure)
            return statusChangeResult;

        return await _repository.SaveAsync(trade, cancellationToken);
    }

    private Result ValidateCommandSchema(UpdateTradeStatusCommand command)
    {
        var commandData = new
        {
            TradeId = command.AggregateId,
            TraderId = "SYSTEM",
            InstrumentId = "UNKNOWN", 
            Quantity = 1, 
            Price = 1.0, 
            Direction = "BUY", 
            TradeDateTime = DateTime.UtcNow, 
            Currency = "USD", 
            Status = command.NewStatus,
            CounterpartyId = "SYSTEM", 
            SourceSystem = "PostTradeSystem", 
            MessageType = "STATUS_UPDATE" 
        };

        var jsonMessage = System.Text.Json.JsonSerializer.Serialize(commandData);
        var isValid = _schemaValidator.ValidateMessage("TradeMessage", jsonMessage);
        if (!isValid)
        {
            return Result.Failure("Schema validation failed for UpdateTradeStatusCommand");
        }

        return Result.Success();
    }

    private static Result ValidateCommand(UpdateTradeStatusCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.AggregateId))
            return Result.Failure("Trade ID is required");

        if (string.IsNullOrWhiteSpace(command.NewStatus))
            return Result.Failure("New status is required");

        if (string.IsNullOrWhiteSpace(command.Reason))
            return Result.Failure("Reason is required");

        return Result.Success();
    }
}

public class EnrichTradeCommandHandler : ICommandHandler<EnrichTradeCommand>
{
    private readonly IAggregateRepository<TradeAggregate> _repository;
    private readonly IJsonSchemaValidator _schemaValidator;

    public EnrichTradeCommandHandler(IAggregateRepository<TradeAggregate> repository, IJsonSchemaValidator schemaValidator)
    {
        _repository = repository;
        _schemaValidator = schemaValidator;
    }

    public async Task<Result> HandleAsync(EnrichTradeCommand command, CancellationToken cancellationToken = default)
    {
        var schemaValidationResult = ValidateCommandSchema(command);
        if (schemaValidationResult.IsFailure)
            return schemaValidationResult;

        var validationResult = ValidateCommand(command);
        if (validationResult.IsFailure)
            return validationResult;

        var tradeResult = await _repository.GetByIdAsync(command.AggregateId, cancellationToken);
        if (tradeResult.IsFailure)
            return Result.Failure($"Failed to load trade: {tradeResult.Error}");

        if (tradeResult.Value == null)
            return Result.Failure($"Trade with ID {command.AggregateId} not found");

        var trade = tradeResult.Value;
        trade.EnrichTrade(command.EnrichmentType, command.EnrichmentData, command.CorrelationId, command.CausedBy);

        return await _repository.SaveAsync(trade, cancellationToken);
    }

    private Result ValidateCommandSchema(EnrichTradeCommand command)
    {
        var commandData = new
        {
            TradeId = command.AggregateId,
            TraderId = "SYSTEM", 
            InstrumentId = "UNKNOWN", 
            Quantity = 1, 
            Price = 1.0, 
            Direction = "BUY", 
            TradeDateTime = DateTime.UtcNow, 
            Currency = "USD", 
            Status = "PENDING", 
            CounterpartyId = "SYSTEM", 
            SourceSystem = "PostTradeSystem", 
            MessageType = "ENRICHMENT" 
        };

        var jsonMessage = System.Text.Json.JsonSerializer.Serialize(commandData);
        var isValid = _schemaValidator.ValidateMessage("TradeMessage", jsonMessage);
        if (!isValid)
        {
            return Result.Failure("Schema validation failed for EnrichTradeCommand");
        }

        return Result.Success();
    }

    private static Result ValidateCommand(EnrichTradeCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.AggregateId))
            return Result.Failure("Trade ID is required");

        if (string.IsNullOrWhiteSpace(command.EnrichmentType))
            return Result.Failure("Enrichment type is required");

        if (command.EnrichmentData == null || !command.EnrichmentData.Any())
            return Result.Failure("Enrichment data is required");

        return Result.Success();
    }
}

public class ValidateTradeCommandHandler : ICommandHandler<ValidateTradeCommand>
{
    private readonly IAggregateRepository<TradeAggregate> _repository;
    private readonly IJsonSchemaValidator _schemaValidator;

    public ValidateTradeCommandHandler(IAggregateRepository<TradeAggregate> repository, IJsonSchemaValidator schemaValidator)
    {
        _repository = repository;
        _schemaValidator = schemaValidator;
    }

    public async Task<Result> HandleAsync(ValidateTradeCommand command, CancellationToken cancellationToken = default)
    {
        var schemaValidationResult = ValidateCommandSchema(command);
        if (schemaValidationResult.IsFailure)
            return schemaValidationResult;

        var validationResult = ValidateCommand(command);
        if (validationResult.IsFailure)
            return validationResult;

        var tradeResult = await _repository.GetByIdAsync(command.AggregateId, cancellationToken);
        if (tradeResult.IsFailure)
            return Result.Failure($"Failed to load trade: {tradeResult.Error}");

        if (tradeResult.Value == null)
            return Result.Failure($"Trade with ID {command.AggregateId} not found");

        var trade = tradeResult.Value;
        var businessValidationErrors = PerformBusinessValidation(trade);

        if (businessValidationErrors.Any())
        {
            trade.RecordValidationFailure(businessValidationErrors, command.CorrelationId, command.CausedBy);
            await _repository.SaveAsync(trade, cancellationToken);
            return Result.Failure($"Trade validation failed: {string.Join(", ", businessValidationErrors)}");
        }

        return Result.Success();
    }

    private Result ValidateCommandSchema(ValidateTradeCommand command)
    {
        var commandData = new
        {
            TradeId = command.AggregateId,
            TraderId = "SYSTEM", 
            InstrumentId = "UNKNOWN", 
            Quantity = 1, 
            Price = 1.0, 
            Direction = "BUY", 
            TradeDateTime = DateTime.UtcNow, 
            Currency = "USD", 
            Status = "PENDING", 
            CounterpartyId = "SYSTEM", 
            SourceSystem = "PostTradeSystem", 
            MessageType = "VALIDATION" 
        };

        var jsonMessage = System.Text.Json.JsonSerializer.Serialize(commandData);
        var isValid = _schemaValidator.ValidateMessage("TradeMessage", jsonMessage);
        if (!isValid)
        {
            return Result.Failure("Schema validation failed for ValidateTradeCommand");
        }

        return Result.Success();
    }

    private static Result ValidateCommand(ValidateTradeCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.AggregateId))
            return Result.Failure("Trade ID is required");

        return Result.Success();
    }

    private static List<string> PerformBusinessValidation(TradeAggregate trade)
    {
        var errors = new List<string>();

        if (trade.Quantity <= 0)
            errors.Add("Trade quantity must be greater than zero");

        if (trade.Price <= 0)
            errors.Add("Trade price must be greater than zero");

        if (trade.TradeDateTime > DateTime.UtcNow.AddMinutes(5))
            errors.Add("Trade date cannot be more than 5 minutes in the future");

        if (trade.TradeDateTime < DateTime.UtcNow.AddDays(-30))
            errors.Add("Trade date cannot be more than 30 days in the past");

        if (string.IsNullOrWhiteSpace(trade.Currency) || trade.Currency.Length != 3)
            errors.Add("Currency must be a valid 3-character code");

        return errors;
    }
}