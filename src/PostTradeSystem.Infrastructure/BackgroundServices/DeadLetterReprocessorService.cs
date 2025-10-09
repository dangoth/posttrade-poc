using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PostTradeSystem.Infrastructure.Services;

namespace PostTradeSystem.Infrastructure.BackgroundServices;

public class DeadLetterReprocessorService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<DeadLetterReprocessorService> _logger;
    private readonly TimeSpan _reprocessingInterval = TimeSpan.FromHours(1); // Check every hour
    private readonly TimeSpan _deadLetterAge = TimeSpan.FromHours(24); // Only reprocess events older than 24 hours

    public DeadLetterReprocessorService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<DeadLetterReprocessorService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dead letter reprocessor service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReprocessOldDeadLettersAsync(stoppingToken);
                await Task.Delay(_reprocessingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Dead letter reprocessor service shutdown requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in dead letter reprocessor service");
                
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Dead letter reprocessor service stopped");
    }

    private async Task ReprocessOldDeadLettersAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();
            
            var deadLetteredEventsResult = await outboxService.GetDeadLetteredEventsAsync(100, stoppingToken);
            if (deadLetteredEventsResult.IsFailure)
            {
                _logger.LogError("Failed to get dead lettered events: {Error}", deadLetteredEventsResult.Error);
                return;
            }

            var deadLetteredEvents = deadLetteredEventsResult.Value!;
            var oldDeadLetters = deadLetteredEvents
                .Where(e => e.DeadLetteredAt.HasValue && 
                           DateTime.UtcNow - e.DeadLetteredAt.Value >= _deadLetterAge)
                .Take(10) // Process max 10 at a time
                .ToList();

            if (oldDeadLetters.Any())
            {
                _logger.LogInformation("Found {Count} old dead lettered events to reprocess", oldDeadLetters.Count);

                foreach (var deadLetter in oldDeadLetters)
                {
                    var reprocessResult = await outboxService.ReprocessDeadLetteredEventAsync(deadLetter.Id, stoppingToken);
                    if (reprocessResult.IsSuccess)
                    {
                        _logger.LogInformation("Automatically reprocessed dead lettered event {EventId} (Age: {Age})", 
                            deadLetter.EventId, DateTime.UtcNow - deadLetter.DeadLetteredAt);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to reprocess dead lettered event {EventId}: {Error}", 
                            deadLetter.EventId, reprocessResult.Error);
                    }
                }
            }
            else
            {
                _logger.LogDebug("No old dead lettered events found for reprocessing");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during dead letter reprocessing cycle");
            throw;
        }
    }
}