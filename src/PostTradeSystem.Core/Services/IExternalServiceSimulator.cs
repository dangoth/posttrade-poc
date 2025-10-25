namespace PostTradeSystem.Core.Services;

public interface IExternalServiceSimulator
{
    void ConfigureLatency(TimeSpan minLatency, TimeSpan maxLatency);
    void ConfigureFailureRate(double failureRate);
    void ConfigureCircuitBreaker(int failureThreshold, TimeSpan recoveryTime);
}

public class ExternalServiceSimulatorConfig
{
    public TimeSpan MinLatency { get; set; } = TimeSpan.FromMilliseconds(10);
    public TimeSpan MaxLatency { get; set; } = TimeSpan.FromMilliseconds(100);
    public double FailureRate { get; set; } = 0.01; // 1% failure rate
    public int CircuitBreakerThreshold { get; set; } = 5;
    public TimeSpan CircuitBreakerRecoveryTime { get; set; } = TimeSpan.FromMinutes(1);
}