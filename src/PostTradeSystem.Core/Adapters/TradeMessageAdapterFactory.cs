using Microsoft.Extensions.Logging;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Messages;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PostTradeSystem.Core.Adapters;

public class TradeMessageAdapterFactory : ITradeMessageAdapterFactory
{
    private readonly ILogger<TradeMessageAdapterFactory> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<string, object> _adapters;

    public TradeMessageAdapterFactory(ILogger<TradeMessageAdapterFactory> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        _adapters = new Dictionary<string, object>
        {
            ["EQUITY"] = new EquityTradeAdapter(),
            ["FX"] = new FxTradeAdapter(),
            ["OPTION"] = new OptionTradeAdapter()
        };
    }

    public async Task<TradeCreatedEvent?> ProcessMessageAsync(string messageType, string sourceSystem, string messageValue, string correlationId)
    {
        var normalizedMessageType = messageType.ToUpperInvariant();
        
        if (!_adapters.TryGetValue(normalizedMessageType, out var adapter))
        {
            _logger.LogWarning("No adapter found for message type: {MessageType}", messageType);
            return null;
        }

        try
        {
            return normalizedMessageType switch
            {
                "EQUITY" => await ProcessWithAdapter((EquityTradeAdapter)adapter, messageValue, sourceSystem, correlationId),
                "FX" => await ProcessWithAdapter((FxTradeAdapter)adapter, messageValue, sourceSystem, correlationId),
                "OPTION" => await ProcessWithAdapter((OptionTradeAdapter)adapter, messageValue, sourceSystem, correlationId),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {MessageType} message from {SourceSystem}", messageType, sourceSystem);
            return null;
        }
    }

    public bool CanProcessMessage(string messageType, string sourceSystem)
    {
        var normalizedMessageType = messageType.ToUpperInvariant();
        
        if (!_adapters.TryGetValue(normalizedMessageType, out var adapter))
            return false;

        return normalizedMessageType switch
        {
            "EQUITY" => ((EquityTradeAdapter)adapter).CanHandle(sourceSystem, normalizedMessageType),
            "FX" => ((FxTradeAdapter)adapter).CanHandle(sourceSystem, normalizedMessageType),
            "OPTION" => ((OptionTradeAdapter)adapter).CanHandle(sourceSystem, normalizedMessageType),
            _ => false
        };
    }

    private async Task<TradeCreatedEvent?> ProcessWithAdapter<T>(
        ITradeMessageAdapter<T> adapter, 
        string messageValue, 
        string sourceSystem, 
        string correlationId) where T : TradeMessage
    {
        if (!adapter.CanHandle(sourceSystem, adapter.MessageType))
        {
            _logger.LogWarning("Adapter {AdapterType} cannot handle source system {SourceSystem}", 
                typeof(T).Name, sourceSystem);
            return null;
        }

        var envelope = JsonSerializer.Deserialize<TradeMessageEnvelope<T>>(messageValue, _jsonOptions);
        if (envelope?.Payload == null)
        {
            _logger.LogWarning("Failed to deserialize {MessageType} envelope", typeof(T).Name);
            return null;
        }

        return await adapter.AdaptToEventAsync(envelope, correlationId);
    }
}