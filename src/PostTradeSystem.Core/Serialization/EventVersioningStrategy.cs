using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization.Contracts;

namespace PostTradeSystem.Core.Serialization;

public class EventVersioningStrategy
{
    private readonly EventSerializationRegistry _registry;

    public EventVersioningStrategy(EventSerializationRegistry registry)
    {
        _registry = registry;
    }

    public void ConfigureEventVersioning()
    {
        RegisterTradeCreatedEventVersions();
        RegisterTradeStatusChangedEventVersions();
        RegisterEventConverters();
    }

    private void RegisterTradeCreatedEventVersions()
    {
        _registry.RegisterContract<TradeCreatedEventV1>(
            domainEvent => MapToTradeCreatedV1((TradeCreatedEvent)domainEvent),
            contract => MapFromTradeCreatedV1(contract)
        );

        _registry.RegisterContract<TradeCreatedEventV2>(
            domainEvent => MapToTradeCreatedV2((TradeCreatedEvent)domainEvent),
            contract => MapFromTradeCreatedV2(contract)
        );
    }

    private void RegisterTradeStatusChangedEventVersions()
    {
        _registry.RegisterContract<TradeStatusChangedEventV1>(
            domainEvent => MapToTradeStatusChangedV1((TradeStatusChangedEvent)domainEvent),
            contract => MapFromTradeStatusChangedV1(contract)
        );

        _registry.RegisterContract<TradeStatusChangedEventV2>(
            domainEvent => MapToTradeStatusChangedV2((TradeStatusChangedEvent)domainEvent),
            contract => MapFromTradeStatusChangedV2(contract)
        );
    }

    private void RegisterEventConverters()
    {
        _registry.RegisterConverter(new TradeCreatedEventV1ToV2Converter());
        _registry.RegisterConverter(new TradeCreatedEventV2ToV1Converter());
        _registry.RegisterConverter(new TradeStatusChangedEventV1ToV2Converter());
        _registry.RegisterConverter(new TradeStatusChangedEventV2ToV1Converter());
    }

    private static TradeCreatedEventV1 MapToTradeCreatedV1(TradeCreatedEvent domainEvent)
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

    private static TradeCreatedEventV2 MapToTradeCreatedV2(TradeCreatedEvent domainEvent)
    {
        return new TradeCreatedEventV2
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
            AdditionalData = new Dictionary<string, object>(domainEvent.AdditionalData),
            RiskProfile = ExtractRiskProfile(domainEvent.AdditionalData),
            NotionalValue = domainEvent.Quantity * domainEvent.Price,
            RegulatoryClassification = DetermineRegulatoryClassification(domainEvent.TradeType)
        };
    }

    private static TradeCreatedEvent MapFromTradeCreatedV1(TradeCreatedEventV1 contract)
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
            contract.AdditionalData
        );
    }

    private static TradeCreatedEvent MapFromTradeCreatedV2(TradeCreatedEventV2 contract)
    {
        var additionalData = new Dictionary<string, object>(contract.AdditionalData)
        {
            ["RiskProfile"] = contract.RiskProfile,
            ["NotionalValue"] = contract.NotionalValue,
            ["RegulatoryClassification"] = contract.RegulatoryClassification
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
            additionalData
        );
    }

    private static TradeStatusChangedEventV1 MapToTradeStatusChangedV1(TradeStatusChangedEvent domainEvent)
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

    private static TradeStatusChangedEventV2 MapToTradeStatusChangedV2(TradeStatusChangedEvent domainEvent)
    {
        return new TradeStatusChangedEventV2
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
            Reason = domainEvent.Reason,
            ApprovedBy = domainEvent.CausedBy,
            ApprovalTimestamp = domainEvent.OccurredAt,
            AuditTrail = $"Status changed from {domainEvent.PreviousStatus} to {domainEvent.NewStatus}. Reason: {domainEvent.Reason}"
        };
    }

    private static TradeStatusChangedEvent MapFromTradeStatusChangedV1(TradeStatusChangedEventV1 contract)
    {
        return new TradeStatusChangedEvent(
            contract.AggregateId,
            contract.PreviousStatus,
            contract.NewStatus,
            contract.Reason,
            contract.AggregateVersion,
            contract.CorrelationId,
            contract.CausedBy
        );
    }

    private static TradeStatusChangedEvent MapFromTradeStatusChangedV2(TradeStatusChangedEventV2 contract)
    {
        return new TradeStatusChangedEvent(
            contract.AggregateId,
            contract.PreviousStatus,
            contract.NewStatus,
            contract.Reason,
            contract.AggregateVersion,
            contract.CorrelationId,
            contract.CausedBy
        );
    }

    private static string ExtractRiskProfile(Dictionary<string, object> additionalData)
    {
        if (additionalData.TryGetValue("RiskProfile", out var riskProfile))
        {
            return riskProfile.ToString() ?? "STANDARD";
        }
        return "STANDARD";
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