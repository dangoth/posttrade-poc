using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PostTradeSystem.Core.Serialization;

namespace PostTradeSystem.Infrastructure.Services;

public class SerializationInitializationService : IHostedService
{
    private readonly ISerializationManagementService _serializationService;
    private readonly ILogger<SerializationInitializationService> _logger;

    public SerializationInitializationService(ISerializationManagementService serializationService, ILogger<SerializationInitializationService> logger)
    {
        _serializationService = serializationService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing serialization services...");
        await _serializationService.InitializeAsync();
        _logger.LogInformation("Serialization services initialized");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}