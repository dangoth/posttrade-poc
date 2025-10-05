using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PostTradeSystem.Infrastructure.BackgroundServices;
using PostTradeSystem.Infrastructure.Services;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.BackgroundServices;

public class OutboxProcessorServiceTests
{
    private readonly Mock<IOutboxService> _mockOutboxService;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ILogger<OutboxProcessorService>> _mockLogger;
    private readonly OutboxProcessorService _service;

    public OutboxProcessorServiceTests()
    {
        _mockOutboxService = new Mock<IOutboxService>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockLogger = new Mock<ILogger<OutboxProcessorService>>();

        _mockServiceScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceScopeFactory.Setup(x => x.CreateScope()).Returns(_mockServiceScope.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IOutboxService))).Returns(_mockOutboxService.Object);

        _service = new OutboxProcessorService(_mockServiceScopeFactory.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProcessOutboxEventsRegularly()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        
        // Cancel after a short delay to stop the background service
        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Act
        await _service.StartAsync(cancellationTokenSource.Token);
        
        await Task.Delay(150);
        
        await _service.StopAsync(cancellationTokenSource.Token);

        // Assert
        _mockOutboxService.Verify(x => x.ProcessOutboxEventsAsync(It.IsAny<CancellationToken>()), 
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleExceptionsGracefully()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(100));

        _mockOutboxService
            .Setup(x => x.ProcessOutboxEventsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act & Assert - Should not throw exception
        await _service.StartAsync(cancellationTokenSource.Token);
        await Task.Delay(150);
        await _service.StopAsync(cancellationTokenSource.Token);

        // Service should continue running despite exceptions
        _mockOutboxService.Verify(x => x.ProcessOutboxEventsAsync(It.IsAny<CancellationToken>()), 
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRetryFailedEventsRegularly()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        
        // Create service with very short intervals for testing
        var testService = new OutboxProcessorService(
            _mockServiceScopeFactory.Object, 
            _mockLogger.Object,
            processingInterval: TimeSpan.FromMilliseconds(10),
            retryInterval: TimeSpan.FromMilliseconds(50));
        
        // Cancel after enough time for retry to be called
        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(200));

        // Act
        await testService.StartAsync(cancellationTokenSource.Token);
        await Task.Delay(250);
        await testService.StopAsync(cancellationTokenSource.Token);

        // Assert - Both processing and retry should be called with short intervals
        _mockOutboxService.Verify(x => x.ProcessOutboxEventsAsync(It.IsAny<CancellationToken>()), 
            Times.AtLeastOnce, "ProcessOutboxEventsAsync should be called by the processing loop");
        
        _mockOutboxService.Verify(x => x.RetryFailedEventsAsync(It.IsAny<CancellationToken>()), 
            Times.AtLeastOnce, "RetryFailedEventsAsync should be called by the retry loop");
    }

}