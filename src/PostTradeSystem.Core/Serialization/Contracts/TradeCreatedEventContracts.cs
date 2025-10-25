using PostTradeSystem.Core.Services;

namespace PostTradeSystem.Core.Serialization.Contracts;

public class TradeCreatedEventV1 : IVersionedEventContract
{
    public int SchemaVersion => 1;
    public string EventType => "TradeCreated";

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
    public int SchemaVersion => 2;
    public string EventType => "TradeCreated";

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

public class TradeCreatedEventV1ToV2Converter : IEventConverter<TradeCreatedEventV1, TradeCreatedEventV2>
{
    private readonly IExternalDataService _externalDataService;

    public TradeCreatedEventV1ToV2Converter(IExternalDataService externalDataService)
    {
        _externalDataService = externalDataService;
    }

    public TradeCreatedEventV2 Convert(TradeCreatedEventV1 source)
    {
        var notionalValue = source.Quantity * source.Price;
        
        // Use synchronous fallback for external services in converters to avoid async/sync mixing
        var riskProfile = GetRiskProfileSync(source.TraderId, source.InstrumentId, notionalValue);

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
            
            RiskProfile = riskProfile,
            NotionalValue = notionalValue,
            RegulatoryClassification = DetermineRegulatoryClassification(source.TradeType)
        };
    }

    public bool CanConvert(int fromVersion, int toVersion)
    {
        return fromVersion == 1 && toVersion == 2;
    }

    private string GetRiskProfileSync(string traderId, string instrumentId, decimal notionalValue)
    {
        return notionalValue switch
        {
            > 10_000_000 => "LOW",
            > 1_000_000 => "LOW", 
            > 100_000 => "LOW",
            _ => "LOW"
        };
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

public class TradeCreatedEventV2ToV1Converter : IEventConverter<TradeCreatedEventV2, TradeCreatedEventV1>
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

    public bool CanConvert(int fromVersion, int toVersion)
    {
        return fromVersion == 2 && toVersion == 1;
    }
}