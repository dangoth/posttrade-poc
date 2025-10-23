using PostTradeSystem.Core.Schemas;
using System.Text.Json;
using Xunit;
using FluentAssertions;

namespace PostTradeSystem.Core.Tests.Schemas;

/// <summary>
/// Tests for event schema validation logic. This class focuses solely on testing
/// that the JsonSchemaValidator correctly validates events against their schemas.
/// Business logic tests should mock the validator and test separately.
/// </summary>
public class EventSchemaValidationTests
{
    private readonly JsonSchemaValidator _validator;

    public EventSchemaValidationTests()
    {
        _validator = new JsonSchemaValidator();
        
        // Register all event schemas using the exact same registration as the real system
        _validator.RegisterSchema("event-tradecreated-v1", EventSchemas.TradeCreatedEventV1Schema);
        _validator.RegisterSchema("event-tradecreated-v2", EventSchemas.TradeCreatedEventV2Schema);
        _validator.RegisterSchema("event-tradestatuschanged-v1", EventSchemas.TradeStatusChangedEventV1Schema);
        _validator.RegisterSchema("event-tradestatuschanged-v2", EventSchemas.TradeStatusChangedEventV2Schema);
    }

    #region TradeCreatedEvent V1 Tests

    [Fact]
    public void ValidateMessage_ValidTradeCreatedEventV1_ShouldReturnTrue()
    {
        // Arrange
        var validEventV1 = """
        {
            "eventId": "evt-001",
            "aggregateId": "trade-001",
            "aggregateType": "Trade",
            "occurredAt": "2025-10-18T09:00:00.000Z",
            "aggregateVersion": 1,
            "correlationId": "corr-001",
            "causedBy": "user-001",
            "traderId": "trader-001",
            "instrumentId": "AAPL",
            "quantity": 100,
            "price": 150.25,
            "direction": "BUY",
            "tradeDateTime": "2025-10-18T09:00:00.000Z",
            "currency": "USD",
            "counterpartyId": "counterparty-001",
            "tradeType": "EQUITY",
            "additionalData": {
                "symbol": "AAPL",
                "exchange": "NASDAQ"
            }
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("event-tradecreated-v1", validEventV1, null);

        // Assert
        isValid.Should().BeTrue("Valid TradeCreatedEvent V1 should pass validation");
    }

    [Fact]
    public void ValidateMessage_TradeCreatedEventV1_MissingRequiredFields_ShouldReturnFalse()
    {
        // Arrange - Missing required fields like eventId, aggregateId
        var invalidEventV1 = """
        {
            "traderId": "trader-001",
            "instrumentId": "AAPL",
            "quantity": 100
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("event-tradecreated-v1", invalidEventV1, null);

        // Assert
        isValid.Should().BeFalse("TradeCreatedEvent V1 missing required fields should fail validation");
    }

    [Fact]
    public void ValidateMessage_TradeCreatedEventV1_InvalidDirection_ShouldReturnFalse()
    {
        // Arrange - Invalid direction (not BUY or SELL)
        var invalidEventV1 = """
        {
            "eventId": "evt-001",
            "aggregateId": "trade-001",
            "aggregateType": "Trade",
            "occurredAt": "2025-10-18T09:00:00.000Z",
            "aggregateVersion": 1,
            "correlationId": "corr-001",
            "causedBy": "user-001",
            "traderId": "trader-001",
            "instrumentId": "AAPL",
            "quantity": 100,
            "price": 150.25,
            "direction": "INVALID_DIRECTION",
            "tradeDateTime": "2025-10-18T09:00:00.000Z",
            "currency": "USD",
            "counterpartyId": "counterparty-001",
            "tradeType": "EQUITY",
            "additionalData": {}
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("event-tradecreated-v1", invalidEventV1, null);

        // Assert
        isValid.Should().BeFalse("TradeCreatedEvent V1 with invalid direction should fail validation");
    }

    [Fact]
    public void ValidateMessage_TradeCreatedEventV1_InvalidTradeType_ShouldReturnFalse()
    {
        // Arrange - Invalid trade type (not EQUITY, OPTION, or FX)
        var invalidEventV1 = """
        {
            "eventId": "evt-001",
            "aggregateId": "trade-001",
            "aggregateType": "Trade",
            "occurredAt": "2025-10-18T09:00:00.000Z",
            "aggregateVersion": 1,
            "correlationId": "corr-001",
            "causedBy": "user-001",
            "traderId": "trader-001",
            "instrumentId": "AAPL",
            "quantity": 100,
            "price": 150.25,
            "direction": "BUY",
            "tradeDateTime": "2025-10-18T09:00:00.000Z",
            "currency": "USD",
            "counterpartyId": "counterparty-001",
            "tradeType": "INVALID_TYPE",
            "additionalData": {}
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("event-tradecreated-v1", invalidEventV1, null);

        // Assert
        isValid.Should().BeFalse("TradeCreatedEvent V1 with invalid trade type should fail validation");
    }

    #endregion

    #region TradeCreatedEvent V2 Tests

    [Fact]
    public void ValidateMessage_ValidTradeCreatedEventV2_ShouldReturnTrue()
    {
        // Arrange - V2 requires riskProfile, notionalValue, regulatoryClassification
        var validEventV2 = """
        {
            "eventId": "evt-002",
            "aggregateId": "trade-002",
            "aggregateType": "Trade",
            "occurredAt": "2025-10-18T09:00:00.000Z",
            "aggregateVersion": 2,
            "correlationId": "corr-002",
            "causedBy": "user-002",
            "traderId": "trader-002",
            "instrumentId": "MSFT",
            "quantity": 200,
            "price": 300.50,
            "direction": "SELL",
            "tradeDateTime": "2025-10-18T09:00:00.000Z",
            "currency": "USD",
            "counterpartyId": "counterparty-002",
            "tradeType": "EQUITY",
            "additionalData": {
                "symbol": "MSFT",
                "exchange": "NASDAQ"
            },
            "riskProfile": "MEDIUM",
            "notionalValue": 60100.00,
            "regulatoryClassification": "MiFID_II"
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("event-tradecreated-v2", validEventV2, null);

        // Assert
        isValid.Should().BeTrue("Valid TradeCreatedEvent V2 should pass validation");
    }

    [Fact]
    public void ValidateMessage_TradeCreatedEventV2_MissingRequiredFields_ShouldReturnFalse()
    {
        // Arrange - V2 requires riskProfile, notionalValue, regulatoryClassification
        var invalidEventV2 = """
        {
            "eventId": "evt-002",
            "aggregateId": "trade-002",
            "aggregateType": "Trade",
            "occurredAt": "2025-10-18T09:00:00.000Z",
            "aggregateVersion": 2,
            "correlationId": "corr-002",
            "causedBy": "user-002",
            "traderId": "trader-002",
            "instrumentId": "MSFT",
            "quantity": 200,
            "price": 300.50,
            "direction": "SELL",
            "tradeDateTime": "2025-10-18T09:00:00.000Z",
            "currency": "USD",
            "counterpartyId": "counterparty-002",
            "tradeType": "EQUITY",
            "additionalData": {}
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("event-tradecreated-v2", invalidEventV2, null);

        // Assert
        isValid.Should().BeFalse("TradeCreatedEvent V2 missing required V2 fields should fail validation");
    }

    #endregion

    #region TradeStatusChangedEvent V1 Tests

    [Fact]
    public void ValidateMessage_ValidTradeStatusChangedEventV1_ShouldReturnTrue()
    {
        // Arrange - V1 schema has: eventId, aggregateId, aggregateType, occurredAt, aggregateVersion, correlationId, causedBy, previousStatus, newStatus, reason
        var validStatusEventV1 = """
        {
            "eventId": "evt-status-001",
            "aggregateId": "trade-001",
            "aggregateType": "Trade",
            "occurredAt": "2025-10-18T09:05:00.000Z",
            "aggregateVersion": 2,
            "correlationId": "corr-status-001",
            "causedBy": "system",
            "previousStatus": "PENDING",
            "newStatus": "EXECUTED",
            "reason": "Trade executed successfully"
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("event-tradestatuschanged-v1", validStatusEventV1, null);

        // Assert
        isValid.Should().BeTrue("Valid TradeStatusChangedEvent V1 should pass validation");
    }

    [Fact]
    public void ValidateMessage_TradeStatusChangedEventV1_MissingReason_ShouldReturnFalse()
    {
        // Arrange - Missing required reason field
        var invalidStatusEventV1 = """
        {
            "eventId": "evt-status-001",
            "aggregateId": "trade-001",
            "aggregateType": "Trade",
            "occurredAt": "2025-10-18T09:05:00.000Z",
            "aggregateVersion": 2,
            "correlationId": "corr-status-001",
            "causedBy": "system",
            "previousStatus": "PENDING",
            "newStatus": "EXECUTED"
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("event-tradestatuschanged-v1", invalidStatusEventV1, null);

        // Assert
        isValid.Should().BeFalse("TradeStatusChangedEvent V1 missing reason should fail validation");
    }

    #endregion

    #region TradeStatusChangedEvent V2 Tests

    [Fact]
    public void ValidateMessage_ValidTradeStatusChangedEventV2_ShouldReturnTrue()
    {
        // Arrange - V2 schema has: eventId, aggregateId, aggregateType, occurredAt, aggregateVersion, correlationId, causedBy, previousStatus, newStatus, reason, approvedBy, approvalTimestamp, auditTrail
        var validStatusEventV2 = """
        {
            "eventId": "evt-status-002",
            "aggregateId": "trade-002",
            "aggregateType": "Trade",
            "occurredAt": "2025-10-18T09:10:00.000Z",
            "aggregateVersion": 3,
            "correlationId": "corr-status-002",
            "causedBy": "settlement-system",
            "previousStatus": "EXECUTED",
            "newStatus": "SETTLED",
            "reason": "Trade settled successfully",
            "approvedBy": "SETTLEMENT_SYSTEM",
            "approvalTimestamp": "2025-10-18T09:10:00.000Z",
            "auditTrail": "AUTO_SETTLEMENT_PROCESS"
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("event-tradestatuschanged-v2", validStatusEventV2, null);

        // Assert
        isValid.Should().BeTrue("Valid TradeStatusChangedEvent V2 should pass validation");
    }

    [Fact]
    public void ValidateMessage_TradeStatusChangedEventV2_MissingRequiredFields_ShouldReturnFalse()
    {
        // Arrange - V2 requires approvedBy, approvalTimestamp, auditTrail fields
        var invalidStatusEventV2 = """
        {
            "eventId": "evt-status-002",
            "aggregateId": "trade-002",
            "aggregateType": "Trade",
            "occurredAt": "2025-10-18T09:10:00.000Z",
            "aggregateVersion": 3,
            "correlationId": "corr-status-002",
            "causedBy": "settlement-system",
            "previousStatus": "EXECUTED",
            "newStatus": "SETTLED",
            "reason": "Trade settled successfully"
        }
        """;

        // Act
        var isValid = _validator.ValidateMessage("event-tradestatuschanged-v2", invalidStatusEventV2, null);

        // Assert
        isValid.Should().BeFalse("TradeStatusChangedEvent V2 missing required V2 fields should fail validation");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ValidateMessage_UnknownEventSchema_ShouldReturnFalse()
    {
        // Arrange
        var eventData = """{"test": "data"}""";

        // Act - Unknown schemas with no version return false
        var isValid = _validator.ValidateMessage("unknown-event-schema", eventData, null);

        // Assert
        isValid.Should().BeFalse("Unknown event schemas with no version should return false");
    }

    [Fact]
    public void ValidateMessage_UnknownEventSchemaWithVersion_ShouldReturnTrue()
    {
        // Arrange
        var eventData = """{"test": "data"}""";

        // Act - Unknown schemas with version return true
        var isValid = _validator.ValidateMessage("unknown-event-schema", eventData, 1);

        // Assert
        isValid.Should().BeTrue("Unknown event schemas with version should return true");
    }

    [Fact]
    public void ValidateMessage_InvalidEventJson_ShouldReturnFalse()
    {
        // Arrange
        var invalidJson = """{"invalid": json syntax""";

        // Act
        var isValid = _validator.ValidateMessage("event-tradecreated-v1", invalidJson, null);

        // Assert
        isValid.Should().BeFalse("Invalid JSON should fail validation");
    }

    #endregion
}