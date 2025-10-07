using Microsoft.Extensions.Logging;
using Moq;
using PostTradeSystem.Infrastructure.Services;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Services;

public class RetryServiceTests
{
    private readonly Mock<ILogger<RetryService>> _mockLogger;
    private readonly RetryService _retryService;

    public RetryServiceTests()
    {
        _mockLogger = new Mock<ILogger<RetryService>>();
        _retryService = new RetryService(_mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_SucceedsOnFirstAttempt_ReturnsResult()
    {
        var expectedResult = "success";
        var operation = () => Task.FromResult(expectedResult);

        var result = await _retryService.ExecuteWithRetryAsync(operation);

        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_SucceedsOnSecondAttempt_ReturnsResult()
    {
        var expectedResult = "success";
        var attemptCount = 0;
        var operation = () =>
        {
            attemptCount++;
            if (attemptCount == 1)
                throw new InvalidOperationException("First attempt fails");
            return Task.FromResult(expectedResult);
        };

        var result = await _retryService.ExecuteWithRetryAsync(operation, maxRetries: 2);

        Assert.Equal(expectedResult, result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ExceedsMaxRetries_ThrowsLastException()
    {
        var attemptCount = 0;
        Func<Task<string>> operation = () =>
        {
            attemptCount++;
            throw new InvalidOperationException($"Attempt {attemptCount} fails");
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _retryService.ExecuteWithRetryAsync(operation, maxRetries: 2));

        Assert.Contains("Attempt 3 fails", exception.Message);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        
        Func<Task<string>> operation = () =>
        {
            cts.Token.ThrowIfCancellationRequested();
            return Task.FromResult("success");
        };
        
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _retryService.ExecuteWithRetryAsync(operation, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_VoidOperation_CompletesSuccessfully()
    {
        var executed = false;
        var operation = () =>
        {
            executed = true;
            return Task.CompletedTask;
        };

        await _retryService.ExecuteWithRetryAsync(operation);

        Assert.True(executed);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_VoidOperationWithRetries_RetriesCorrectly()
    {
        var attemptCount = 0;
        var operation = () =>
        {
            attemptCount++;
            if (attemptCount < 3)
                throw new InvalidOperationException($"Attempt {attemptCount} fails");
            return Task.CompletedTask;
        };

        await _retryService.ExecuteWithRetryAsync(operation, maxRetries: 3);

        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_CustomBaseDelay_UsesProvidedDelay()
    {
        var attemptCount = 0;
        var startTime = DateTime.UtcNow;
        var operation = () =>
        {
            attemptCount++;
            if (attemptCount == 1)
                throw new InvalidOperationException("First attempt fails");
            return Task.FromResult("success");
        };

        await _retryService.ExecuteWithRetryAsync(operation, maxRetries: 2, baseDelay: TimeSpan.FromMilliseconds(100));

        var elapsed = DateTime.UtcNow - startTime;
        Assert.True(elapsed >= TimeSpan.FromMilliseconds(90));
        Assert.Equal(2, attemptCount);
    }
}