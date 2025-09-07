namespace PostTradeSystem.Core.Conventions;

public static class EventNamingConventions
{
    public const string EVENT_SUFFIX = "Event";
    public const string COMMAND_SUFFIX = "Command";
    
    public static class EventTypes
    {
        public const string TRADE_CREATED = "TradeCreated";
        public const string TRADE_STATUS_CHANGED = "TradeStatusChanged";
        public const string TRADE_UPDATED = "TradeUpdated";
        public const string TRADE_ENRICHED = "TradeEnriched";
        public const string TRADE_VALIDATION_FAILED = "TradeValidationFailed";
    }
    
    public static class CommandTypes
    {
        public const string CREATE_TRADE = "CreateTrade";
        public const string UPDATE_TRADE_STATUS = "UpdateTradeStatus";
        public const string ENRICH_TRADE = "EnrichTrade";
        public const string VALIDATE_TRADE = "ValidateTrade";
    }
    
    public static class CorrelationStrategies
    {
        public const string TRADE_LIFECYCLE = "trade-lifecycle";
        public const string BATCH_PROCESSING = "batch-processing";
        public const string EXTERNAL_ENRICHMENT = "external-enrichment";
        public const string REGULATORY_REPORTING = "regulatory-reporting";
    }
    
    public static class Topics
    {
        public const string TRADES_EQUITIES = "trades.equities";
        public const string TRADES_OPTIONS = "trades.options";
        public const string TRADES_FX = "trades.fx";
        public const string REFERENCE_INSTRUMENTS = "reference.instruments";
        public const string REFERENCE_TRADERS = "reference.traders";
        
        public static string GetTopicForTradeType(string tradeType)
        {
            return tradeType.ToUpper() switch
            {
                "EQUITY" => TRADES_EQUITIES,
                "OPTION" => TRADES_OPTIONS,
                "FX" => TRADES_FX,
                _ => throw new ArgumentException($"Unknown trade type: {tradeType}")
            };
        }
    }
    
    public static class PartitionKeys
    {
        public static string GetTradePartitionKey(string tradeId)
        {
            return $"trade-{tradeId}";
        }
        
        public static string GetTraderPartitionKey(string traderId)
        {
            return $"trader-{traderId}";
        }
        
        public static string GetInstrumentPartitionKey(string instrumentId)
        {
            return $"instrument-{instrumentId}";
        }
    }
    
    public static class Headers
    {
        public const string EVENT_TYPE = "event-type";
        public const string EVENT_VERSION = "event-version";
        public const string CORRELATION_ID = "correlation-id";
        public const string CAUSATION_ID = "causation-id";
        public const string AGGREGATE_ID = "aggregate-id";
        public const string AGGREGATE_TYPE = "aggregate-type";
        public const string AGGREGATE_VERSION = "aggregate-version";
        public const string SOURCE_SYSTEM = "source-system";
        public const string TIMESTAMP = "timestamp";
        public const string SCHEMA_VERSION = "schema-version";
    }
    
    public static string GetEventTypeName(Type eventType)
    {
        var typeName = eventType.Name;
        return typeName.EndsWith(EVENT_SUFFIX) 
            ? typeName[..^EVENT_SUFFIX.Length] 
            : typeName;
    }
    
    public static string GetCommandTypeName(Type commandType)
    {
        var typeName = commandType.Name;
        return typeName.EndsWith(COMMAND_SUFFIX) 
            ? typeName[..^COMMAND_SUFFIX.Length] 
            : typeName;
    }
    
    public static string GenerateCorrelationId(string strategy, string identifier)
    {
        return $"{strategy}-{identifier}-{Guid.NewGuid():N}";
    }
}