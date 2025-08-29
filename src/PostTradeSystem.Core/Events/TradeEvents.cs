namespace PostTradeSystem.Core.Events;

public class TradeCreatedEvent : DomainEventBase
{
    public TradeCreatedEvent(
        string tradeId, 
        string traderId, 
        string instrumentId, 
        decimal quantity, 
        decimal price, 
        string direction, 
        DateTime tradeDateTime, 
        string currency, 
        string counterpartyId,
        string tradeType,
        long aggregateVersion, 
        string correlationId, 
        string causedBy,
        Dictionary<string, object> additionalData) 
        : base(tradeId, "Trade", aggregateVersion, correlationId, causedBy)
    {
        TraderId = traderId;
        InstrumentId = instrumentId;
        Quantity = quantity;
        Price = price;
        Direction = direction;
        TradeDateTime = tradeDateTime;
        Currency = currency;
        CounterpartyId = counterpartyId;
        TradeType = tradeType;
        AdditionalData = additionalData;
    }

    public string TraderId { get; private set; }
    public string InstrumentId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal Price { get; private set; }
    public string Direction { get; private set; }
    public DateTime TradeDateTime { get; private set; }
    public string Currency { get; private set; }
    public string CounterpartyId { get; private set; }
    public string TradeType { get; private set; }
    public Dictionary<string, object> AdditionalData { get; private set; }
}

public class TradeStatusChangedEvent : DomainEventBase
{
    public TradeStatusChangedEvent(
        string tradeId, 
        string previousStatus, 
        string newStatus, 
        string reason,
        long aggregateVersion, 
        string correlationId, 
        string causedBy) 
        : base(tradeId, "Trade", aggregateVersion, correlationId, causedBy)
    {
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        Reason = reason;
    }

    public string PreviousStatus { get; private set; }
    public string NewStatus { get; private set; }
    public string Reason { get; private set; }
}

public class TradeUpdatedEvent : DomainEventBase
{
    public TradeUpdatedEvent(
        string tradeId, 
        Dictionary<string, object> updatedFields,
        long aggregateVersion, 
        string correlationId, 
        string causedBy) 
        : base(tradeId, "Trade", aggregateVersion, correlationId, causedBy)
    {
        UpdatedFields = updatedFields;
    }

    public Dictionary<string, object> UpdatedFields { get; private set; }
}

public class TradeEnrichedEvent : DomainEventBase
{
    public TradeEnrichedEvent(
        string tradeId, 
        string enrichmentType, 
        Dictionary<string, object> enrichmentData,
        long aggregateVersion, 
        string correlationId, 
        string causedBy) 
        : base(tradeId, "Trade", aggregateVersion, correlationId, causedBy)
    {
        EnrichmentType = enrichmentType;
        EnrichmentData = enrichmentData;
    }

    public string EnrichmentType { get; private set; }
    public Dictionary<string, object> EnrichmentData { get; private set; }
}

public class TradeValidationFailedEvent : DomainEventBase
{
    public TradeValidationFailedEvent(
        string tradeId, 
        List<string> validationErrors,
        long aggregateVersion, 
        string correlationId, 
        string causedBy) 
        : base(tradeId, "Trade", aggregateVersion, correlationId, causedBy)
    {
        ValidationErrors = validationErrors;
    }

    public List<string> ValidationErrors { get; private set; }
}