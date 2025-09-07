namespace PostTradeSystem.Core.Commands;

public class CreateTradeCommand : CommandBase
{
    public CreateTradeCommand(
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
        Dictionary<string, object> additionalData,
        string correlationId,
        string causedBy) : base(tradeId, correlationId, causedBy)
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

public class UpdateTradeStatusCommand : CommandBase
{
    public UpdateTradeStatusCommand(
        string tradeId,
        string newStatus,
        string reason,
        string correlationId,
        string causedBy) : base(tradeId, correlationId, causedBy)
    {
        NewStatus = newStatus;
        Reason = reason;
    }

    public string NewStatus { get; private set; }
    public string Reason { get; private set; }
}

public class EnrichTradeCommand : CommandBase
{
    public EnrichTradeCommand(
        string tradeId,
        string enrichmentType,
        Dictionary<string, object> enrichmentData,
        string correlationId,
        string causedBy) : base(tradeId, correlationId, causedBy)
    {
        EnrichmentType = enrichmentType;
        EnrichmentData = enrichmentData;
    }

    public string EnrichmentType { get; private set; }
    public Dictionary<string, object> EnrichmentData { get; private set; }
}

public class ValidateTradeCommand : CommandBase
{
    public ValidateTradeCommand(
        string tradeId,
        string correlationId,
        string causedBy) : base(tradeId, correlationId, causedBy)
    {
    }
}