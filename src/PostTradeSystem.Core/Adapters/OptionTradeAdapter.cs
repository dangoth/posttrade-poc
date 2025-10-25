using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Messages;
using PostTradeSystem.Core.Services;

namespace PostTradeSystem.Core.Adapters;

public class OptionTradeAdapter : ITradeMessageAdapter<OptionTradeMessage>
{
    private readonly IExternalDataService _externalDataService;

    public OptionTradeAdapter(IExternalDataService externalDataService)
    {
        _externalDataService = externalDataService;
    }

    public string SourceSystem => "OPTION_SYSTEM";
    public string MessageType => "OPTION";

    public async Task<TradeCreatedEvent?> AdaptToEventAsync(TradeMessageEnvelope<OptionTradeMessage> envelope, string correlationId)
    {
        if (envelope?.Payload == null)
            return null;

        var notionalValue = envelope.Payload.Quantity * envelope.Payload.Price;

        // Enrich with external data services
        var riskProfile = await _externalDataService.GetRiskAssessmentScoreAsync(
            envelope.Payload.TraderId, envelope.Payload.InstrumentId, notionalValue);
        var accountHolderDetails = await _externalDataService.GetAccountHolderDetailsAsync(envelope.Payload.TraderId);
        var isCompliant = await _externalDataService.ValidateRegulatoryComplianceAsync(
            "OPTION", envelope.Payload.CounterpartyId, notionalValue);
        var volatilityFactor = await _externalDataService.GetMarketDataEnrichmentAsync(
            envelope.Payload.InstrumentId, envelope.Payload.TradeDateTime);

        var additionalData = new Dictionary<string, object>
        {
            ["UnderlyingSymbol"] = envelope.Payload.UnderlyingSymbol ?? string.Empty,
            ["StrikePrice"] = envelope.Payload.StrikePrice,
            ["ExpirationDate"] = envelope.Payload.ExpirationDate,
            ["OptionType"] = envelope.Payload.OptionType ?? string.Empty,
            ["Exchange"] = envelope.Payload.Exchange ?? string.Empty,
            ["ImpliedVolatility"] = envelope.Payload.ImpliedVolatility,
            ["ContractSize"] = envelope.Payload.ContractSize ?? string.Empty,
            ["SettlementType"] = envelope.Payload.SettlementType ?? string.Empty,
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
            "OPTION",
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