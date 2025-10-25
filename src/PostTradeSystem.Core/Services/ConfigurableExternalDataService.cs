namespace PostTradeSystem.Core.Services;

public class ConfigurableExternalDataService : IExternalDataService, IExternalServiceSimulator
{
    private readonly IExternalDataService _mockService;
    private ExternalServiceSimulatorConfig _config = new();
    private readonly Random _random = new();
    private int _consecutiveFailures = 0;
    private DateTime _circuitBreakerOpenedAt = DateTime.MinValue;

    public ConfigurableExternalDataService(IExternalDataService mockService)
    {
        _mockService = mockService;
    }

    public void ConfigureLatency(TimeSpan minLatency, TimeSpan maxLatency)
    {
        _config.MinLatency = minLatency;
        _config.MaxLatency = maxLatency;
    }

    public void ConfigureFailureRate(double failureRate)
    {
        _config.FailureRate = Math.Clamp(failureRate, 0.0, 1.0);
    }

    public void ConfigureCircuitBreaker(int failureThreshold, TimeSpan recoveryTime)
    {
        _config.CircuitBreakerThreshold = failureThreshold;
        _config.CircuitBreakerRecoveryTime = recoveryTime;
    }

    public async Task<string> GetRiskAssessmentScoreAsync(string traderId, string instrumentId, decimal notionalValue)
    {
        var isSuccessful = await SimulateLatencyAndFailures();
        if (!isSuccessful)
        {
            return "STANDARD"; // Fallback value when service fails
        }
        return await _mockService.GetRiskAssessmentScoreAsync(traderId, instrumentId, notionalValue);
    }

    public async Task<string> GetAccountHolderDetailsAsync(string traderId)
    {
        var isSuccessful = await SimulateLatencyAndFailures();
        if (!isSuccessful)
        {
            return "RETAIL"; // Fallback value when service fails
        }
        return await _mockService.GetAccountHolderDetailsAsync(traderId);
    }

    public async Task<bool> ValidateRegulatoryComplianceAsync(string tradeType, string counterpartyId, decimal notionalValue)
    {
        var isSuccessful = await SimulateLatencyAndFailures();
        if (!isSuccessful)
        {
            return true; // Fallback to compliant when service fails (conservative approach)
        }
        return await _mockService.ValidateRegulatoryComplianceAsync(tradeType, counterpartyId, notionalValue);
    }

    public async Task<decimal> GetMarketDataEnrichmentAsync(string instrumentId, DateTime tradeDateTime)
    {
        var isSuccessful = await SimulateLatencyAndFailures();
        if (!isSuccessful)
        {
            return 0.25m; // Fallback volatility factor when service fails
        }
        return await _mockService.GetMarketDataEnrichmentAsync(instrumentId, tradeDateTime);
    }

    private async Task<bool> SimulateLatencyAndFailures()
    {
        // Check circuit breaker
        if (_consecutiveFailures >= _config.CircuitBreakerThreshold)
        {
            if (DateTime.UtcNow - _circuitBreakerOpenedAt < _config.CircuitBreakerRecoveryTime)
            {
                return false; // Circuit breaker open
            }
            _consecutiveFailures = 0; // Reset after recovery time
        }

        // Simulate latency
        var latencyMs = _random.Next(
            (int)_config.MinLatency.TotalMilliseconds,
            (int)_config.MaxLatency.TotalMilliseconds);
        
        if (latencyMs > 0)
        {
            await Task.Delay(latencyMs);
        }

        // Simulate failures
        if (_random.NextDouble() < _config.FailureRate)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= _config.CircuitBreakerThreshold)
            {
                _circuitBreakerOpenedAt = DateTime.UtcNow;
            }
            return false; // Simulated failure
        }

        _consecutiveFailures = 0;
        return true; // Success
    }
}