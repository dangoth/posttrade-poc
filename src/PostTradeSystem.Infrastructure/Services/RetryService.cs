using Microsoft.Extensions.Logging;

namespace PostTradeSystem.Infrastructure.Services;

public interface IRetryService
{
    Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        TimeSpan? baseDelay = null,
        CancellationToken cancellationToken = default);
    
    Task ExecuteWithRetryAsync(
        Func<Task> operation,
        int maxRetries = 3,
        TimeSpan? baseDelay = null,
        CancellationToken cancellationToken = default);
}

public class RetryService : IRetryService
{
    private readonly ILogger<RetryService> _logger;
    private static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromSeconds(1);

    public RetryService(ILogger<RetryService> logger)
    {
        _logger = logger;
    }

    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        TimeSpan? baseDelay = null,
        CancellationToken cancellationToken = default)
    {
        var delay = baseDelay ?? DefaultBaseDelay;
        Exception lastException = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                
                if (attempt == maxRetries)
                {
                    _logger.LogError(ex, "Operation failed after {MaxRetries} attempts", maxRetries);
                    break;
                }

                var currentDelay = CalculateExponentialBackoff(delay, attempt);
                _logger.LogWarning(ex, "Operation failed on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms", 
                    attempt + 1, maxRetries + 1, currentDelay.TotalMilliseconds);

                await Task.Delay(currentDelay, cancellationToken);
            }
        }

        throw lastException ?? new InvalidOperationException("Operation failed without exception");
    }

    public async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        int maxRetries = 3,
        TimeSpan? baseDelay = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await operation();
            return true;
        }, maxRetries, baseDelay, cancellationToken);
    }

    private static TimeSpan CalculateExponentialBackoff(TimeSpan baseDelay, int attempt)
    {
        var exponentialDelay = TimeSpan.FromMilliseconds(
            baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
        
        var jitter = Random.Shared.NextDouble() * 0.1;
        var jitteredDelay = TimeSpan.FromMilliseconds(
            exponentialDelay.TotalMilliseconds * (1 + jitter));
        
        return TimeSpan.FromMilliseconds(Math.Min(jitteredDelay.TotalMilliseconds, 30000));
    }
}