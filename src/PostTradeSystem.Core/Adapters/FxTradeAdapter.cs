using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Messages;
using PostTradeSystem.Core.Services;

namespace PostTradeSystem.Core.Adapters;

public class FxTradeAdapter : ITradeMessageAdapter<FxTradeMessage>
{
    private readonly IExternalDataService _externalDataService;

    public FxTradeAdapter(IExternalDataService externalDataService)
    {
        _externalDataService = externalDataService;
    }

    public string SourceSystem => "FX_SYSTEM";
    public string MessageType => "FX";

    public async Task<TradeCreatedEvent?> AdaptToEventAsync(TradeMessageEnvelope<FxTradeMessage> envelope, string correlationId)
    {
        if (envelope?.Payload == null)
            return null;

        var notionalValue = envelope.Payload.Quantity * envelope.Payload.Price;

        // Enrich with external data services
        var riskProfile = await _externalDataService.GetRiskAssessmentScoreAsync(
            envelope.Payload.TraderId, envelope.Payload.InstrumentId, notionalValue);
        var accountHolderDetails = await _externalDataService.GetAccountHolderDetailsAsync(envelope.Payload.TraderId);
        var isCompliant = await _externalDataService.ValidateRegulatoryComplianceAsync(
            "FX", envelope.Payload.CounterpartyId, notionalValue);
        var volatilityFactor = await _externalDataService.GetMarketDataEnrichmentAsync(
            envelope.Payload.InstrumentId, envelope.Payload.TradeDateTime);

        var additionalData = new Dictionary<string, object>
        {
            ["BaseCurrency"] = envelope.Payload.BaseCurrency ?? string.Empty,
            ["QuoteCurrency"] = envelope.Payload.QuoteCurrency ?? string.Empty,
            ["SettlementDate"] = envelope.Payload.SettlementDate,
            ["SpotRate"] = envelope.Payload.SpotRate,
            ["ForwardPoints"] = envelope.Payload.ForwardPoints,
            ["TradeType"] = envelope.Payload.TradeType ?? string.Empty,
            ["DeliveryMethod"] = envelope.Payload.DeliveryMethod ?? string.Empty,
            ["SourceSystem"] = envelope.Payload.SourceSystem ?? string.Empty,
            // External enrichment data
            ["RiskProfile"] = riskProfile,
            ["AccountHolderType"] = accountHolderDetails,
            ["RegulatoryCompliant"] = isCompliant,
            ["VolatilityFactor"] = volatilityFactor,
            ["NotionalValue"] = notionalValue
        };

        var tradeEvent = new TradeCreatedEvent(
            envelope.Payload.TradeId,
            envelope.Payload.TraderId,
            envelope.Payload.InstrumentId,
            envelope.Payload.Quantity,
            envelope.Payload.Price,
            envelope.Payload.Direction,
            envelope.Payload.TradeDateTime,
            envelope.Payload.Currency,
            envelope.Payload.CounterpartyId,
            "FX",
            1,
            correlationId,
            "KafkaConsumerService",
            additionalData);

        return tradeEvent;
    }

    public bool CanHandle(string sourceSystem, string messageType)
    {
        return (string.IsNullOrEmpty(sourceSystem) || sourceSystem == SourceSystem) && 
               messageType.ToUpperInvariant() == MessageType;
    }
}