namespace PostTradeSystem.Core.Schemas;

public static class EventSchemas
{
    public const string TradeCreatedEventV1Schema = """
    {
        "type": "object",
        "properties": {
            "eventId": { "type": "string", "required": true },
            "aggregateId": { "type": "string", "required": true },
            "aggregateType": { "type": "string", "required": true },
            "occurredAt": { "type": "string", "format": "date-time", "required": true },
            "aggregateVersion": { "type": "number", "required": true },
            "correlationId": { "type": "string", "required": true },
            "causedBy": { "type": "string", "required": true },
            "traderId": { "type": "string", "required": true },
            "instrumentId": { "type": "string", "required": true },
            "quantity": { "type": "number", "required": true },
            "price": { "type": "number", "required": true },
            "direction": { "type": "string", "enum": ["BUY", "SELL"], "required": true },
            "tradeDateTime": { "type": "string", "format": "date-time", "required": true },
            "currency": { "type": "string", "required": true },
            "counterpartyId": { "type": "string", "required": true },
            "tradeType": { "type": "string", "enum": ["EQUITY", "OPTION", "FX"], "required": true },
            "additionalData": { "type": "object", "required": false }
        }
    }
    """;

    public const string TradeCreatedEventV2Schema = """
    {
        "type": "object",
        "properties": {
            "eventId": { "type": "string", "required": true },
            "aggregateId": { "type": "string", "required": true },
            "aggregateType": { "type": "string", "required": true },
            "occurredAt": { "type": "string", "format": "date-time", "required": true },
            "aggregateVersion": { "type": "number", "required": true },
            "correlationId": { "type": "string", "required": true },
            "causedBy": { "type": "string", "required": true },
            "traderId": { "type": "string", "required": true },
            "instrumentId": { "type": "string", "required": true },
            "quantity": { "type": "number", "required": true },
            "price": { "type": "number", "required": true },
            "direction": { "type": "string", "enum": ["BUY", "SELL"], "required": true },
            "tradeDateTime": { "type": "string", "format": "date-time", "required": true },
            "currency": { "type": "string", "required": true },
            "counterpartyId": { "type": "string", "required": true },
            "tradeType": { "type": "string", "enum": ["EQUITY", "OPTION", "FX"], "required": true },
            "additionalData": { "type": "object", "required": false },
            "riskProfile": { "type": "string", "enum": ["LOW", "MEDIUM", "HIGH", "STANDARD"], "required": true },
            "notionalValue": { "type": "number", "required": true },
            "regulatoryClassification": { "type": "string", "required": true }
        }
    }
    """;

    public const string TradeStatusChangedEventV1Schema = """
    {
        "type": "object",
        "properties": {
            "eventId": { "type": "string", "required": true },
            "aggregateId": { "type": "string", "required": true },
            "aggregateType": { "type": "string", "required": true },
            "occurredAt": { "type": "string", "format": "date-time", "required": true },
            "aggregateVersion": { "type": "number", "required": true },
            "correlationId": { "type": "string", "required": true },
            "causedBy": { "type": "string", "required": true },
            "previousStatus": { "type": "string", "enum": ["PENDING", "EXECUTED", "SETTLED", "FAILED", "CANCELLED"], "required": true },
            "newStatus": { "type": "string", "enum": ["PENDING", "EXECUTED", "SETTLED", "FAILED", "CANCELLED"], "required": true },
            "reason": { "type": "string", "required": true }
        }
    }
    """;

    public const string TradeStatusChangedEventV2Schema = """
    {
        "type": "object",
        "properties": {
            "eventId": { "type": "string", "required": true },
            "aggregateId": { "type": "string", "required": true },
            "aggregateType": { "type": "string", "required": true },
            "occurredAt": { "type": "string", "format": "date-time", "required": true },
            "aggregateVersion": { "type": "number", "required": true },
            "correlationId": { "type": "string", "required": true },
            "causedBy": { "type": "string", "required": true },
            "previousStatus": { "type": "string", "enum": ["PENDING", "EXECUTED", "SETTLED", "FAILED", "CANCELLED"], "required": true },
            "newStatus": { "type": "string", "enum": ["PENDING", "EXECUTED", "SETTLED", "FAILED", "CANCELLED"], "required": true },
            "reason": { "type": "string", "required": true },
            "approvedBy": { "type": "string", "required": true },
            "approvalTimestamp": { "type": "string", "format": "date-time", "required": true },
            "auditTrail": { "type": "string", "required": true }
        }
    }
    """;

    public const string ComplianceOutputSchema = """
    {
        "type": "object",
        "properties": {
            "outputId": { "type": "string", "required": true },
            "generatedAt": { "type": "string", "format": "date-time", "required": true },
            "department": { "type": "string", "enum": ["COMPLIANCE"], "required": true },
            "reportingPeriod": { "type": "string", "required": true },
            "metadata": { "type": "object", "required": false },
            "trades": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "tradeId": { "type": "string", "required": true },
                        "traderId": { "type": "string", "required": true },
                        "instrumentId": { "type": "string", "required": true },
                        "notionalValue": { "type": "number", "required": true },
                        "regulatoryClassification": { "type": "string", "required": true },
                        "riskProfile": { "type": "string", "required": true },
                        "tradeDateTime": { "type": "string", "format": "date-time", "required": true },
                        "status": { "type": "string", "required": true },
                        "complianceFlags": { "type": "array", "items": { "type": "string" }, "required": false },
                        "regulatoryData": { "type": "object", "required": false }
                    }
                },
                "required": true
            },
            "summary": {
                "type": "object",
                "properties": {
                    "totalTrades": { "type": "number", "required": true },
                    "totalNotionalValue": { "type": "number", "required": true },
                    "tradesByClassification": { "type": "object", "required": true },
                    "tradesByRiskProfile": { "type": "object", "required": true },
                    "alertCount": { "type": "number", "required": true }
                },
                "required": true
            },
            "alerts": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "alertId": { "type": "string", "required": true },
                        "tradeId": { "type": "string", "required": true },
                        "alertType": { "type": "string", "required": true },
                        "severity": { "type": "string", "enum": ["LOW", "MEDIUM", "HIGH", "CRITICAL"], "required": true },
                        "description": { "type": "string", "required": true },
                        "detectedAt": { "type": "string", "format": "date-time", "required": true },
                        "alertData": { "type": "object", "required": false }
                    }
                },
                "required": true
            }
        }
    }
    """;

    public const string RiskManagementOutputSchema = """
    {
        "type": "object",
        "properties": {
            "outputId": { "type": "string", "required": true },
            "generatedAt": { "type": "string", "format": "date-time", "required": true },
            "department": { "type": "string", "enum": ["RISK_MANAGEMENT"], "required": true },
            "reportingPeriod": { "type": "string", "required": true },
            "metadata": { "type": "object", "required": false },
            "trades": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "tradeId": { "type": "string", "required": true },
                        "traderId": { "type": "string", "required": true },
                        "instrumentId": { "type": "string", "required": true },
                        "quantity": { "type": "number", "required": true },
                        "price": { "type": "number", "required": true },
                        "notionalValue": { "type": "number", "required": true },
                        "direction": { "type": "string", "enum": ["BUY", "SELL"], "required": true },
                        "currency": { "type": "string", "required": true },
                        "tradeType": { "type": "string", "required": true },
                        "riskWeight": { "type": "number", "required": true },
                        "vaR": { "type": "number", "required": true },
                        "greekValues": { "type": "object", "required": false }
                    }
                },
                "required": true
            },
            "metrics": {
                "type": "object",
                "properties": {
                    "totalVaR": { "type": "number", "required": true },
                    "totalNotional": { "type": "number", "required": true },
                    "vaRByAssetClass": { "type": "object", "required": true },
                    "exposureByCounterparty": { "type": "object", "required": true },
                    "exposureByCurrency": { "type": "object", "required": true }
                },
                "required": true
            },
            "exposures": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "exposureType": { "type": "string", "required": true },
                        "identifier": { "type": "string", "required": true },
                        "grossExposure": { "type": "number", "required": true },
                        "netExposure": { "type": "number", "required": true },
                        "limit": { "type": "number", "required": true },
                        "utilizationPercentage": { "type": "number", "required": true }
                    }
                },
                "required": true
            }
        }
    }
    """;
}