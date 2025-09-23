using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PostTradeSystem.Infrastructure.Repositories;

namespace PostTradeSystem.Infrastructure.BackgroundServices;

public class IdempotencyCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IdempotencyCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(24);

    public IdempotencyCleanupService(
        IServiceProvider serviceProvider,
        ILogger<IdempotencyCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var nextCleanup = now.Date.AddDays(1).AddHours(6);
                
                if (now.Hour >= 6)
                {
                    nextCleanup = now.Date.AddDays(1).AddHours(6);
                }
                else
                {
                    nextCleanup = now.Date.AddHours(6);
                }

                var delay = nextCleanup - now;
                
                _logger.LogInformation("Next idempotency cleanup scheduled for {NextCleanup} (in {Delay})", 
                    nextCleanup, delay);

                await Task.Delay(delay, stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                    break;

                await PerformCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Idempotency cleanup service shutdown requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in idempotency cleanup service");
                
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task PerformCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var eventStoreRepository = scope.ServiceProvider.GetRequiredService<IEventStoreRepository>();

            _logger.LogInformation("Starting daily idempotency keys cleanup");

            await eventStoreRepository.CleanupExpiredIdempotencyKeysAsync(cancellationToken);

            _logger.LogInformation("Daily idempotency keys cleanup completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform idempotency keys cleanup");
            throw;
        }
    }
}