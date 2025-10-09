using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PostTradeSystem.Infrastructure.Services;

namespace PostTradeSystem.Infrastructure.BackgroundServices;

public class OutboxProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<OutboxProcessorService> _logger;
    private readonly TimeSpan _processingInterval;
    private readonly TimeSpan _retryInterval;

    public OutboxProcessorService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<OutboxProcessorService> logger,
        TimeSpan? processingInterval = null,
        TimeSpan? retryInterval = null)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _processingInterval = processingInterval ?? TimeSpan.FromSeconds(30);
        _retryInterval = retryInterval ?? TimeSpan.FromMinutes(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor service started");

        var processingTask = ProcessOutboxEventsLoop(stoppingToken);
        
        var retryTask = RetryFailedEventsLoop(stoppingToken);

        await Task.WhenAny(processingTask, retryTask);

        _logger.LogInformation("Outbox processor service stopped");
    }

    private async Task ProcessOutboxEventsLoop(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();
                var result = await outboxService.ProcessOutboxEventsAsync(stoppingToken);
                if (result.IsFailure)
                {
                    _logger.LogError("Failed to process outbox events: {Error}", result.Error);
                }
                await Task.Delay(_processingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Outbox processing loop shutdown requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox processing loop");
                
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task RetryFailedEventsLoop(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_retryInterval, stoppingToken);
                
                if (!stoppingToken.IsCancellationRequested)
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();
                    var result = await outboxService.RetryFailedEventsAsync(stoppingToken);
                    if (result.IsFailure)
                    {
                        _logger.LogError("Failed to retry failed events: {Error}", result.Error);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Outbox retry loop shutdown requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox retry loop");
                
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}