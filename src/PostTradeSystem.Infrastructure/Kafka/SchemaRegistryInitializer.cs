using PostTradeSystem.Core.Schemas;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PostTradeSystem.Infrastructure.Kafka;

public class SchemaRegistryInitializer : IHostedService
{
    private readonly ISchemaRegistry _schemaRegistry;
    private readonly ILogger<SchemaRegistryInitializer> _logger;

    public SchemaRegistryInitializer(ISchemaRegistry schemaRegistry, ILogger<SchemaRegistryInitializer> logger)
    {
        _schemaRegistry = schemaRegistry;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing schema registry with predefined schemas");

        try
        {
            await _schemaRegistry.RegisterSchemaAsync("trade-equity-value", MessageSchemas.EquityTradeMessageSchema);
            await _schemaRegistry.RegisterSchemaAsync("trade-option-value", MessageSchemas.OptionTradeMessageSchema);
            await _schemaRegistry.RegisterSchemaAsync("trade-fx-value", MessageSchemas.FxTradeMessageSchema);
            await _schemaRegistry.RegisterSchemaAsync("trade-envelope-value", MessageSchemas.TradeMessageEnvelopeSchema);

            _logger.LogInformation("Schema registry initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize schema registry");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}