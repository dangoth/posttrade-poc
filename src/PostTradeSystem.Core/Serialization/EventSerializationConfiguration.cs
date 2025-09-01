using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization.Contracts;

namespace PostTradeSystem.Core.Serialization;

public static class EventSerializationConfiguration
{
    public static void Configure(EventSerializationRegistry registry)
    {
        ConfigureTradeCreatedEvent(registry);
        ConfigureTradeStatusChangedEvent(registry);
        ConfigureTradeUpdatedEvent(registry);
        ConfigureTradeEnrichedEvent(registry);
        ConfigureTradeValidationFailedEvent(registry);
    }

    private static void ConfigureTradeCreatedEvent(EventSerializationRegistry registry)
    {
        registry.RegisterContract(
            domainEvent => ConvertTradeCreatedEventToV1((TradeCreatedEvent)domainEvent),
            contract => ConvertV1ToTradeCreatedEvent(contract));

        registry.RegisterContract(
            domainEvent => ConvertTradeCreatedEventToV2((TradeCreatedEvent)domainEvent),
            contract => ConvertV2ToTradeCreatedEvent(contract));

        registry.RegisterConverter(new TradeCreatedEventV1ToV2Converter());
        registry.RegisterConverter(new TradeCreatedEventV2ToV1Converter());
    }

    private static void ConfigureTradeStatusChangedEvent(EventSerializationRegistry registry)
    {
        registry.RegisterContract(
            domainEvent => ConvertTradeStatusChangedEventToV1((TradeStatusChangedEvent)domainEvent),
            contract => ConvertV1ToTradeStatusChangedEvent(contract));

        registry.RegisterContract(
            domainEvent => ConvertTradeStatusChangedEventToV2((TradeStatusChangedEvent)domainEvent),
            contract => ConvertV2ToTradeStatusChangedEvent(contract));

        registry.RegisterConverter(new TradeStatusChangedEventV1ToV2Converter());
        registry.RegisterConverter(new TradeStatusChangedEventV2ToV1Converter());
    }

    private static void ConfigureTradeUpdatedEvent(EventSerializationRegistry registry)
    {
        registry.RegisterContract(
            domainEvent => ConvertTradeUpdatedEventToV1((TradeUpdatedEvent)domainEvent),
            contract => ConvertV1ToTradeUpdatedEvent(contract));
    }

    private static void ConfigureTradeEnrichedEvent(EventSerializationRegistry registry)
    {
        registry.RegisterContract(
            domainEvent => ConvertTradeEnrichedEventToV1((TradeEnrichedEvent)domainEvent),
            contract => ConvertV1ToTradeEnrichedEvent(contract));
    }

    private static void ConfigureTradeValidationFailedEvent(EventSerializationRegistry registry)
    {
        registry.RegisterContract(
            domainEvent => ConvertTradeValidationFailedEventToV1((TradeValidationFailedEvent)domainEvent),
            contract => ConvertV1ToTradeValidationFailedEvent(contract));
    }

    private static TradeCreatedEventV1 ConvertTradeCreatedEventToV1(TradeCreatedEvent domainEvent)
    {
        return new TradeCreatedEventV1
        {
            EventId = domainEvent.EventId,
            AggregateId = domainEvent.AggregateId,
            AggregateType = domainEvent.AggregateType,
            OccurredAt = domainEvent.OccurredAt,
            AggregateVersion = domainEvent.AggregateVersion,
            CorrelationId = domainEvent.CorrelationId,
            CausedBy = domainEvent.CausedBy,
            TraderId = domainEvent.TraderId,
            InstrumentId = domainEvent.InstrumentId,
            Quantity = domainEvent.Quantity,
            Price = domainEvent.Price,
            Direction = domainEvent.Direction,
            TradeDateTime = domainEvent.TradeDateTime,
            Currency = domainEvent.Currency,
            CounterpartyId = domainEvent.CounterpartyId,
            TradeType = domainEvent.TradeType,
            AdditionalData = new Dictionary<string, object>(domainEvent.AdditionalData)
        };
    }

    private static TradeCreatedEventV2 ConvertTradeCreatedEventToV2(TradeCreatedEvent domainEvent)
    {
        var v1 = ConvertTradeCreatedEventToV1(domainEvent);
        var converter = new TradeCreatedEventV1ToV2Converter();
        return converter.Convert(v1);
    }

    private static TradeCreatedEvent ConvertV1ToTradeCreatedEvent(TradeCreatedEventV1 contract)
    {
        return new TradeCreatedEvent(
            contract.AggregateId,
            contract.TraderId,
            contract.InstrumentId,
            contract.Quantity,
            contract.Price,
            contract.Direction,
            contract.TradeDateTime,
            contract.Currency,
            contract.CounterpartyId,
            contract.TradeType,
            contract.AggregateVersion,
            contract.CorrelationId,
            contract.CausedBy,
            contract.AdditionalData);
    }

    private static TradeCreatedEvent ConvertV2ToTradeCreatedEvent(TradeCreatedEventV2 contract)
    {
        var additionalData = new Dictionary<string, object>(contract.AdditionalData)
        {
            ["riskProfile"] = contract.RiskProfile,
            ["notionalValue"] = contract.NotionalValue,
            ["regulatoryClassification"] = contract.RegulatoryClassification
        };

        return new TradeCreatedEvent(
            contract.AggregateId,
            contract.TraderId,
            contract.InstrumentId,
            contract.Quantity,
            contract.Price,
            contract.Direction,
            contract.TradeDateTime,
            contract.Currency,
            contract.CounterpartyId,
            contract.TradeType,
            contract.AggregateVersion,
            contract.CorrelationId,
            contract.CausedBy,
            additionalData);
    }

    private static TradeStatusChangedEventV1 ConvertTradeStatusChangedEventToV1(TradeStatusChangedEvent domainEvent)
    {
        return new TradeStatusChangedEventV1
        {
            EventId = domainEvent.EventId,
            AggregateId = domainEvent.AggregateId,
            AggregateType = domainEvent.AggregateType,
            OccurredAt = domainEvent.OccurredAt,
            AggregateVersion = domainEvent.AggregateVersion,
            CorrelationId = domainEvent.CorrelationId,
            CausedBy = domainEvent.CausedBy,
            PreviousStatus = domainEvent.PreviousStatus,
            NewStatus = domainEvent.NewStatus,
            Reason = domainEvent.Reason
        };
    }

    private static TradeStatusChangedEventV2 ConvertTradeStatusChangedEventToV2(TradeStatusChangedEvent domainEvent)
    {
        var v1 = ConvertTradeStatusChangedEventToV1(domainEvent);
        var converter = new TradeStatusChangedEventV1ToV2Converter();
        return converter.Convert(v1);
    }

    private static TradeStatusChangedEvent ConvertV1ToTradeStatusChangedEvent(TradeStatusChangedEventV1 contract)
    {
        return new TradeStatusChangedEvent(
            contract.AggregateId,
            contract.PreviousStatus,
            contract.NewStatus,
            contract.Reason,
            contract.AggregateVersion,
            contract.CorrelationId,
            contract.CausedBy);
    }

    private static TradeStatusChangedEvent ConvertV2ToTradeStatusChangedEvent(TradeStatusChangedEventV2 contract)
    {
        return new TradeStatusChangedEvent(
            contract.AggregateId,
            contract.PreviousStatus,
            contract.NewStatus,
            contract.Reason,
            contract.AggregateVersion,
            contract.CorrelationId,
            contract.CausedBy);
    }

    private static TradeUpdatedEventV1 ConvertTradeUpdatedEventToV1(TradeUpdatedEvent domainEvent)
    {
        return new TradeUpdatedEventV1
        {
            EventId = domainEvent.EventId,
            AggregateId = domainEvent.AggregateId,
            AggregateType = domainEvent.AggregateType,
            OccurredAt = domainEvent.OccurredAt,
            AggregateVersion = domainEvent.AggregateVersion,
            CorrelationId = domainEvent.CorrelationId,
            CausedBy = domainEvent.CausedBy,
            UpdatedFields = new Dictionary<string, object>(domainEvent.UpdatedFields)
        };
    }

    private static TradeUpdatedEvent ConvertV1ToTradeUpdatedEvent(TradeUpdatedEventV1 contract)
    {
        return new TradeUpdatedEvent(
            contract.AggregateId,
            contract.UpdatedFields,
            contract.AggregateVersion,
            contract.CorrelationId,
            contract.CausedBy);
    }

    private static TradeEnrichedEventV1 ConvertTradeEnrichedEventToV1(TradeEnrichedEvent domainEvent)
    {
        return new TradeEnrichedEventV1
        {
            EventId = domainEvent.EventId,
            AggregateId = domainEvent.AggregateId,
            AggregateType = domainEvent.AggregateType,
            OccurredAt = domainEvent.OccurredAt,
            AggregateVersion = domainEvent.AggregateVersion,
            CorrelationId = domainEvent.CorrelationId,
            CausedBy = domainEvent.CausedBy,
            EnrichmentType = domainEvent.EnrichmentType,
            EnrichmentData = new Dictionary<string, object>(domainEvent.EnrichmentData)
        };
    }

    private static TradeEnrichedEvent ConvertV1ToTradeEnrichedEvent(TradeEnrichedEventV1 contract)
    {
        return new TradeEnrichedEvent(
            contract.AggregateId,
            contract.EnrichmentType,
            contract.EnrichmentData,
            contract.AggregateVersion,
            contract.CorrelationId,
            contract.CausedBy);
    }

    private static TradeValidationFailedEventV1 ConvertTradeValidationFailedEventToV1(TradeValidationFailedEvent domainEvent)
    {
        return new TradeValidationFailedEventV1
        {
            EventId = domainEvent.EventId,
            AggregateId = domainEvent.AggregateId,
            AggregateType = domainEvent.AggregateType,
            OccurredAt = domainEvent.OccurredAt,
            AggregateVersion = domainEvent.AggregateVersion,
            CorrelationId = domainEvent.CorrelationId,
            CausedBy = domainEvent.CausedBy,
            ValidationErrors = new List<string>(domainEvent.ValidationErrors)
        };
    }

    private static TradeValidationFailedEvent ConvertV1ToTradeValidationFailedEvent(TradeValidationFailedEventV1 contract)
    {
        return new TradeValidationFailedEvent(
            contract.AggregateId,
            contract.ValidationErrors,
            contract.AggregateVersion,
            contract.CorrelationId,
            contract.CausedBy);
    }
}

public class TradeUpdatedEventV1 : VersionedEventContractBase
{
    public override int Version => 1;
    public override string EventType => "TradeUpdated";

    public string EventId { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public long AggregateVersion { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string CausedBy { get; set; } = string.Empty;
    public Dictionary<string, object> UpdatedFields { get; set; } = new();
}

public class TradeEnrichedEventV1 : VersionedEventContractBase
{
    public override int Version => 1;
    public override string EventType => "TradeEnriched";

    public string EventId { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public long AggregateVersion { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string CausedBy { get; set; } = string.Empty;
    public string EnrichmentType { get; set; } = string.Empty;
    public Dictionary<string, object> EnrichmentData { get; set; } = new();
}

public class TradeValidationFailedEventV1 : VersionedEventContractBase
{
    public override int Version => 1;
    public override string EventType => "TradeValidationFailed";

    public string EventId { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public long AggregateVersion { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string CausedBy { get; set; } = string.Empty;
    public List<string> ValidationErrors { get; set; } = new();
}