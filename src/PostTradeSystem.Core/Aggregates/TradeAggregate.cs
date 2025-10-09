using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Models;
using PostTradeSystem.Core.Common;

namespace PostTradeSystem.Core.Aggregates;

public class TradeAggregate : AggregateRootBase
{
    private string _traderId = string.Empty;
    private string _instrumentId = string.Empty;
    private decimal _quantity;
    private decimal _price;
    private TradeDirection _direction;
    private DateTime _tradeDateTime;
    private string _currency = string.Empty;
    private TradeStatus _status;
    private string _counterpartyId = string.Empty;
    private string _tradeType = string.Empty;
    private Dictionary<string, object> _additionalData = new();
    private List<string> _validationErrors = new();

    private TradeAggregate(string tradeId, string partitionKey) : base(tradeId, partitionKey)
    {
    }

    public static TradeAggregate CreateTrade(
        string tradeId,
        string traderId,
        string instrumentId,
        decimal quantity,
        decimal price,
        TradeDirection direction,
        DateTime tradeDateTime,
        string currency,
        string counterpartyId,
        string tradeType,
        Dictionary<string, object> additionalData,
        string correlationId,
        string causedBy)
    {
        var partitionKey = GeneratePartitionKey(traderId, instrumentId);
        var trade = new TradeAggregate(tradeId, partitionKey);
        
        var tradeCreatedEvent = new TradeCreatedEvent(
            tradeId,
            traderId,
            instrumentId,
            quantity,
            price,
            direction.ToString(),
            tradeDateTime,
            currency,
            counterpartyId,
            tradeType,
            1, // First version
            correlationId,
            causedBy,
            additionalData);

        trade.ApplyEvent(tradeCreatedEvent);
        return trade;
    }

    public static TradeAggregate FromHistory(string tradeId, string partitionKey, IEnumerable<IDomainEvent> events)
    {
        var trade = new TradeAggregate(tradeId, partitionKey);
        trade.LoadFromHistory(events);
        return trade;
    }

    public static string GeneratePartitionKey(string traderId, string instrumentId)
    {
        return $"{traderId}:{instrumentId}";
    }

    public Result ChangeStatus(TradeStatus newStatus, string reason, string correlationId, string causedBy)
    {
        if (_status == newStatus)
            return Result.Success();

        var validationResult = ValidateStatusTransition(newStatus);
        if (validationResult.IsFailure)
            return validationResult;

        var statusChangedEvent = CreateTradeEvent(
            (version, corrId, causedByParam) => new TradeStatusChangedEvent(
                Id, _status.ToString(), newStatus.ToString(), reason, version, corrId, causedByParam),
            correlationId, causedBy);

        ApplyEvent(statusChangedEvent);
        return Result.Success();
    }

    public Result UpdateTradeDetails(Dictionary<string, object> updates, string correlationId, string causedBy)
    {
        var validationResult = ValidateTradeCanBeUpdated();
        if (validationResult.IsFailure)
            return validationResult;

        var tradeUpdatedEvent = CreateTradeEvent(
            (version, corrId, causedByParam) => new TradeUpdatedEvent(Id, updates, version, corrId, causedByParam),
            correlationId, causedBy);

        ApplyEvent(tradeUpdatedEvent);
        return Result.Success();
    }

    public void EnrichTrade(string enrichmentType, Dictionary<string, object> enrichmentData, string correlationId, string causedBy)
    {
        var tradeEnrichedEvent = CreateTradeEvent(
            (version, corrId, causedByParam) => new TradeEnrichedEvent(Id, enrichmentType, enrichmentData, version, corrId, causedByParam),
            correlationId, causedBy);

        ApplyEvent(tradeEnrichedEvent);
    }

    public void RecordValidationFailure(List<string> validationErrors, string correlationId, string causedBy)
    {
        var validationFailedEvent = CreateTradeEvent(
            (version, corrId, causedByParam) => new TradeValidationFailedEvent(Id, validationErrors, version, corrId, causedByParam),
            correlationId, causedBy);

        ApplyEvent(validationFailedEvent);
    }

    private Result ValidateStatusTransition(TradeStatus newStatus)
    {
        if (newStatus != TradeStatus.Failed && (int)newStatus < (int)_status)
        {
            return Result.Failure($"Cannot change status from {_status} to {newStatus}");
        }
        return Result.Success();
    }

    private Result ValidateTradeCanBeUpdated()
    {
        if (_status == TradeStatus.Settled)
        {
            return Result.Failure("Cannot update a settled trade");
        }
        return Result.Success();
    }

    private T CreateTradeEvent<T>(Func<long, string, string, T> eventFactory, string correlationId, string causedBy) 
        where T : IDomainEvent
    {
        return eventFactory(_eventSequence + 1, correlationId, causedBy);
    }

    protected override void ApplyEventToState(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case TradeCreatedEvent e:
                _traderId = e.TraderId;
                _instrumentId = e.InstrumentId;
                _quantity = e.Quantity;
                _price = e.Price;
                _direction = Enum.Parse<TradeDirection>(e.Direction);
                _tradeDateTime = e.TradeDateTime;
                _currency = e.Currency;
                _counterpartyId = e.CounterpartyId;
                _tradeType = e.TradeType;
                _additionalData = new Dictionary<string, object>(e.AdditionalData);
                _status = TradeStatus.Pending;
                break;

            case TradeStatusChangedEvent e:
                _status = Enum.Parse<TradeStatus>(e.NewStatus);
                break;

            case TradeUpdatedEvent e:
                foreach (var update in e.UpdatedFields)
                {
                    _additionalData[update.Key] = update.Value;
                }
                break;

            case TradeEnrichedEvent e:
                foreach (var enrichment in e.EnrichmentData)
                {
                    _additionalData[$"{e.EnrichmentType}_{enrichment.Key}"] = enrichment.Value;
                }
                break;

            case TradeValidationFailedEvent e:
                _validationErrors.AddRange(e.ValidationErrors);
                _status = TradeStatus.Failed;
                break;
        }
    }

    public string TraderId => _traderId;
    public string InstrumentId => _instrumentId;
    public decimal Quantity => _quantity;
    public decimal Price => _price;
    public TradeDirection Direction => _direction;
    public DateTime TradeDateTime => _tradeDateTime;
    public string Currency => _currency;
    public TradeStatus Status => _status;
    public string CounterpartyId => _counterpartyId;
    public string TradeType => _tradeType;
    public IReadOnlyDictionary<string, object> AdditionalData => _additionalData.AsReadOnly();
    public IReadOnlyList<string> ValidationErrors => _validationErrors.AsReadOnly();
}