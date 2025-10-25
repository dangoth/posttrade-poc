using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Messages;
using PostTradeSystem.Core.Services;

namespace PostTradeSystem.Core.Adapters;

public class EquityTradeAdapter : ITradeMessageAdapter<EquityTradeMessage>
{
    private readonly IExternalDataService _externalDataService;

    public EquityTradeAdapter(IExternalDataService externalDataService)
    {
        _externalDataService = externalDataService;
    }

    public string SourceSystem => "EQUITY_SYSTEM";
    public string MessageType => "EQUITY";

    public async Task<TradeCreatedEvent?> AdaptToEventAsync(TradeMessageEnvelope<EquityTradeMessage> envelope, string correlationId)
    {
        if (envelope?.Payload == null)
            return null;

        var notionalValue = envelope.Payload.Quantity * envelope.Payload.Price;

        // Enrich with external data services
        var riskProfile = await _externalDataService.GetRiskAssessmentScoreAsync(
            envelope.Payload.TraderId, envelope.Payload.InstrumentId, notionalValue);
        var accountHolderDetails = await _externalDataService.GetAccountHolderDetailsAsync(envelope.Payload.TraderId);
        var isCompliant = await _externalDataService.ValidateRegulatoryComplianceAsync(
            "EQUITY", envelope.Payload.CounterpartyId, notionalValue);
        var volatilityFactor = await _externalDataService.GetMarketDataEnrichmentAsync(
            envelope.Payload.InstrumentId, envelope.Payload.TradeDateTime);

        var additionalData = new Dictionary<string, object>
        {
            ["Symbol"] = envelope.Payload.Symbol ?? string.Empty,
            ["Exchange"] = envelope.Payload.Exchange ?? string.Empty,
            ["Sector"] = envelope.Payload.Sector ?? string.Empty,
            ["DividendRate"] = envelope.Payload.DividendRate,
            ["Isin"] = envelope.Payload.Isin ?? string.Empty,
            ["MarketSegment"] = envelope.Payload.MarketSegment ?? string.Empty,
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
            "EQUITY",
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