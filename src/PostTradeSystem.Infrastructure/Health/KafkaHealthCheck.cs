using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PostTradeSystem.Infrastructure.Health;

public class KafkaHealthCheck : IHealthCheck
{
    private readonly KafkaHealthService _kafkaHealthService;

    public KafkaHealthCheck(KafkaHealthService kafkaHealthService)
    {
        _kafkaHealthService = kafkaHealthService;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var status = _kafkaHealthService.GetHealthStatus();
        
        return Task.FromResult(status.IsHealthy 
            ? HealthCheckResult.Healthy(status.Message)
            : HealthCheckResult.Degraded(status.Message));
    }
}

public class KafkaHealthService
{
    private volatile KafkaHealthStatus _currentStatus = new(false, "Kafka consumer not started");

    public void SetHealthy(string message = "Kafka consumer is running")
    {
        _currentStatus = new KafkaHealthStatus(true, message);
    }

    public void SetDegraded(string message)
    {
        _currentStatus = new KafkaHealthStatus(false, message);
    }

    public KafkaHealthStatus GetHealthStatus() => _currentStatus;
}

public record KafkaHealthStatus(bool IsHealthy, string Message);