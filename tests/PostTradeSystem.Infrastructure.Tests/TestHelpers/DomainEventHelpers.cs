using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Models;

namespace PostTradeSystem.Infrastructure.Tests.TestHelpers;

public static class DomainEventHelpers
{
    public static TradeCreatedEvent CreateTradeCreatedEvent(
        string? tradeId = null,
        long aggregateVersion = 1,
        string? correlationId = null,
        string? causedBy = null)
    {
        return new TradeCreatedEvent(
            tradeId ?? Guid.NewGuid().ToString(),
            "TRADER001",
            "AAPL",
            100m,
            150.50m,
            TradeDirection.Buy.ToString(),
            DateTime.UtcNow,
            "USD",
            "COUNTERPARTY001",
            "EQUITY",
            aggregateVersion,
            correlationId ?? Guid.NewGuid().ToString(),
            causedBy ?? "IntegrationTest",
            new Dictionary<string, object> { { "TestData", "Value" } }
        );
    }

    public static TradeStatusChangedEvent CreateTradeStatusChangedEvent(
        string? tradeId = null,
        long aggregateVersion = 2,
        string? correlationId = null,
        string? causedBy = null)
    {
        return new TradeStatusChangedEvent(
            tradeId ?? Guid.NewGuid().ToString(),
            TradeStatus.Pending.ToString(),
            TradeStatus.Executed.ToString(),
            "Trade confirmed by system",
            aggregateVersion,
            correlationId ?? Guid.NewGuid().ToString(),
            causedBy ?? "IntegrationTest"
        );
    }

    public static TradeUpdatedEvent CreateTradeUpdatedEvent(
        string? tradeId = null,
        long aggregateVersion = 3,
        string? correlationId = null,
        string? causedBy = null)
    {
        return new TradeUpdatedEvent(
            tradeId ?? Guid.NewGuid().ToString(),
            new Dictionary<string, object> { { "UpdatedField", "UpdatedValue" } },
            aggregateVersion,
            correlationId ?? Guid.NewGuid().ToString(),
            causedBy ?? "IntegrationTest"
        );
    }
}