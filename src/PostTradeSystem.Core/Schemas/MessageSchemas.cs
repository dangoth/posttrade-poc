namespace PostTradeSystem.Core.Schemas;

public static class MessageSchemas
{
    public const string TradeMessageSchema = """
    {
        "type": "object",
        "properties": {
            "tradeId": { "type": "string", "required": true },
            "traderId": { "type": "string", "required": true },
            "instrumentId": { "type": "string", "required": true },
            "quantity": { "type": "number", "required": true },
            "price": { "type": "number", "required": true },
            "direction": { "type": "string", "enum": ["BUY", "SELL"], "required": true },
            "tradeDateTime": { "type": "string", "format": "date-time", "required": true },
            "currency": { "type": "string", "required": true },
            "status": { "type": "string", "enum": ["PENDING", "EXECUTED", "SETTLED", "FAILED"], "required": true },
            "counterpartyId": { "type": "string", "required": true },
            "sourceSystem": { "type": "string", "required": true },
            "messageType": { "type": "string", "required": true }
        }
    }
    """;

    public const string EquityTradeMessageSchema = """
    {
        "allOf": [
            {
                "type": "object",
                "properties": {
                    "tradeId": { "type": "string", "required": true },
                    "traderId": { "type": "string", "required": true },
                    "instrumentId": { "type": "string", "required": true },
                    "quantity": { "type": "number", "required": true },
                    "price": { "type": "number", "required": true },
                    "direction": { "type": "string", "enum": ["BUY", "SELL"], "required": true },
                    "tradeDateTime": { "type": "string", "format": "date-time", "required": true },
                    "currency": { "type": "string", "required": true },
                    "status": { "type": "string", "enum": ["PENDING", "EXECUTED", "SETTLED", "FAILED"], "required": true },
                    "counterpartyId": { "type": "string", "required": true },
                    "sourceSystem": { "type": "string", "required": true },
                    "messageType": { "type": "string", "enum": ["EQUITY"], "required": true }
                }
            },
            {
                "type": "object",
                "properties": {
                    "symbol": { "type": "string", "required": true },
                    "exchange": { "type": "string", "required": true },
                    "sector": { "type": "string", "required": false },
                    "dividendRate": { "type": "number", "required": false },
                    "isin": { "type": "string", "required": false },
                    "marketSegment": { "type": "string", "required": false }
                }
            }
        ]
    }
    """;

    public const string OptionTradeMessageSchema = """
    {
        "allOf": [
            {
                "type": "object",
                "properties": {
                    "tradeId": { "type": "string", "required": true },
                    "traderId": { "type": "string", "required": true },
                    "instrumentId": { "type": "string", "required": true },
                    "quantity": { "type": "number", "required": true },
                    "price": { "type": "number", "required": true },
                    "direction": { "type": "string", "enum": ["BUY", "SELL"], "required": true },
                    "tradeDateTime": { "type": "string", "format": "date-time", "required": true },
                    "currency": { "type": "string", "required": true },
                    "status": { "type": "string", "enum": ["PENDING", "EXECUTED", "SETTLED", "FAILED"], "required": true },
                    "counterpartyId": { "type": "string", "required": true },
                    "sourceSystem": { "type": "string", "required": true },
                    "messageType": { "type": "string", "enum": ["OPTION"], "required": true }
                }
            },
            {
                "type": "object",
                "properties": {
                    "underlyingSymbol": { "type": "string", "required": true },
                    "strikePrice": { "type": "number", "required": true },
                    "expirationDate": { "type": "string", "format": "date-time", "required": true },
                    "optionType": { "type": "string", "enum": ["CALL", "PUT"], "required": true },
                    "exchange": { "type": "string", "required": true },
                    "impliedVolatility": { "type": "number", "required": false },
                    "contractSize": { "type": "string", "required": false },
                    "settlementType": { "type": "string", "enum": ["PHYSICAL", "CASH"], "required": false }
                }
            }
        ]
    }
    """;

    public const string FxTradeMessageSchema = """
    {
        "allOf": [
            {
                "type": "object",
                "properties": {
                    "tradeId": { "type": "string", "required": true },
                    "traderId": { "type": "string", "required": true },
                    "instrumentId": { "type": "string", "required": true },
                    "quantity": { "type": "number", "required": true },
                    "price": { "type": "number", "required": true },
                    "direction": { "type": "string", "enum": ["BUY", "SELL"], "required": true },
                    "tradeDateTime": { "type": "string", "format": "date-time", "required": true },
                    "currency": { "type": "string", "required": true },
                    "status": { "type": "string", "enum": ["PENDING", "EXECUTED", "SETTLED", "FAILED"], "required": true },
                    "counterpartyId": { "type": "string", "required": true },
                    "sourceSystem": { "type": "string", "required": true },
                    "messageType": { "type": "string", "enum": ["FX"], "required": true }
                }
            },
            {
                "type": "object",
                "properties": {
                    "baseCurrency": { "type": "string", "required": true },
                    "quoteCurrency": { "type": "string", "required": true },
                    "settlementDate": { "type": "string", "format": "date-time", "required": true },
                    "spotRate": { "type": "number", "required": true },
                    "forwardPoints": { "type": "number", "required": false },
                    "tradeType": { "type": "string", "enum": ["SPOT", "FORWARD", "SWAP"], "required": true },
                    "deliveryMethod": { "type": "string", "enum": ["PVP", "CORRESPONDENT"], "required": false }
                }
            }
        ]
    }
    """;

    public const string TradeMessageEnvelopeSchema = """
    {
        "type": "object",
        "properties": {
            "messageId": { "type": "string", "required": true },
            "timestamp": { "type": "string", "format": "date-time", "required": true },
            "version": { "type": "string", "required": true },
            "correlationId": { "type": "string", "required": false },
            "payload": { "type": "object", "required": true },
            "headers": { "type": "object", "required": false }
        }
    }
    """;
}