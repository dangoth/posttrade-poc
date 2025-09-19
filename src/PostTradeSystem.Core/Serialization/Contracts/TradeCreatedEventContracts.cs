namespace PostTradeSystem.Core.Serialization.Contracts;

public class TradeCreatedEventV1 : IVersionedEventContract
{
    public int SchemaVersion { get; set; } = 1;
    public string EventType { get; set; } = "TradeCreated";

    public string EventId { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public long AggregateVersion { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string CausedBy { get; set; } = string.Empty;
    
    // Trade-specific properties
    public string TraderId { get; set; } = string.Empty;
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public string Direction { get; set; } = string.Empty;
    public DateTime TradeDateTime { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string CounterpartyId { get; set; } = string.Empty;
    public string TradeType { get; set; } = string.Empty;
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

public class TradeCreatedEventV2 : IVersionedEventContract
{
    public int SchemaVersion { get; set; } = 2;
    public string EventType { get; set; } = "TradeCreated";

    public string EventId { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public long AggregateVersion { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string CausedBy { get; set; } = string.Empty;
    
    // Trade-specific properties
    public string TraderId { get; set; } = string.Empty;
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public string Direction { get; set; } = string.Empty;
    public DateTime TradeDateTime { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string CounterpartyId { get; set; } = string.Empty;
    public string TradeType { get; set; } = string.Empty;
    public Dictionary<string, object> AdditionalData { get; set; } = new();
    
    public string RiskProfile { get; set; } = string.Empty;
    public decimal NotionalValue { get; set; }
    public string RegulatoryClassification { get; set; } = string.Empty;
}

public class TradeCreatedEventV1ToV2Converter : IEventVersionConverter<TradeCreatedEventV1, TradeCreatedEventV2>
{
    public TradeCreatedEventV2 Convert(TradeCreatedEventV1 source)
    {
        return new TradeCreatedEventV2
        {
            EventId = source.EventId,
            AggregateId = source.AggregateId,
            AggregateType = source.AggregateType,
            OccurredAt = source.OccurredAt,
            AggregateVersion = source.AggregateVersion,
            CorrelationId = source.CorrelationId,
            CausedBy = source.CausedBy,
            TraderId = source.TraderId,
            InstrumentId = source.InstrumentId,
            Quantity = source.Quantity,
            Price = source.Price,
            Direction = source.Direction,
            TradeDateTime = source.TradeDateTime,
            Currency = source.Currency,
            CounterpartyId = source.CounterpartyId,
            TradeType = source.TradeType,
            AdditionalData = new Dictionary<string, object>(source.AdditionalData),
            
            RiskProfile = "STANDARD",
            NotionalValue = source.Quantity * source.Price,
            RegulatoryClassification = DetermineRegulatoryClassification(source.TradeType)
        };
    }

    public bool CanConvert(int fromSchemaVersion, int toSchemaVersion)
    {
        return fromSchemaVersion == 1 && toSchemaVersion == 2;
    }

    private static string DetermineRegulatoryClassification(string tradeType)
    {
        return tradeType.ToUpper() switch
        {
            "EQUITY" => "MiFID_II_EQUITY",
            "OPTION" => "MiFID_II_DERIVATIVE",
            "FX" => "EMIR_FX",
            _ => "UNCLASSIFIED"
        };
    }
}

public class TradeCreatedEventV2ToV1Converter : IEventVersionConverter<TradeCreatedEventV2, TradeCreatedEventV1>
{
    public TradeCreatedEventV1 Convert(TradeCreatedEventV2 source)
    {
        return new TradeCreatedEventV1
        {
            EventId = source.EventId,
            AggregateId = source.AggregateId,
            AggregateType = source.AggregateType,
            OccurredAt = source.OccurredAt,
            AggregateVersion = source.AggregateVersion,
            CorrelationId = source.CorrelationId,
            CausedBy = source.CausedBy,
            TraderId = source.TraderId,
            InstrumentId = source.InstrumentId,
            Quantity = source.Quantity,
            Price = source.Price,
            Direction = source.Direction,
            TradeDateTime = source.TradeDateTime,
            Currency = source.Currency,
            CounterpartyId = source.CounterpartyId,
            TradeType = source.TradeType,
            AdditionalData = new Dictionary<string, object>(source.AdditionalData)
        };
    }

    public bool CanConvert(int fromSchemaVersion, int toSchemaVersion)
    {
        return fromSchemaVersion == 2 && toSchemaVersion == 1;
    }
}