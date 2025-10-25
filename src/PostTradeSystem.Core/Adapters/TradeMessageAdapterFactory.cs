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
    private readonly EquityTradeAdapter _equityAdapter;
    private readonly FxTradeAdapter _fxAdapter;
    private readonly OptionTradeAdapter _optionAdapter;

    public TradeMessageAdapterFactory(
        ILogger<TradeMessageAdapterFactory> logger,
        EquityTradeAdapter equityAdapter,
        FxTradeAdapter fxAdapter,
        OptionTradeAdapter optionAdapter)
    {
        _logger = logger;
        _equityAdapter = equityAdapter;
        _fxAdapter = fxAdapter;
        _optionAdapter = optionAdapter;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public async Task<TradeCreatedEvent?> ProcessMessageAsync(string messageType, string sourceSystem, string messageValue, string correlationId)
    {
        var normalizedMessageType = messageType.ToUpperInvariant();

        try
        {
            return normalizedMessageType switch
            {
                "EQUITY" => await ProcessWithAdapter(_equityAdapter, messageValue, sourceSystem, correlationId),
                "FX" => await ProcessWithAdapter(_fxAdapter, messageValue, sourceSystem, correlationId),
                "OPTION" => await ProcessWithAdapter(_optionAdapter, messageValue, sourceSystem, correlationId),
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

        return normalizedMessageType switch
        {
            "EQUITY" => _equityAdapter.CanHandle(sourceSystem, normalizedMessageType),
            "FX" => _fxAdapter.CanHandle(sourceSystem, normalizedMessageType),
            "OPTION" => _optionAdapter.CanHandle(sourceSystem, normalizedMessageType),
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