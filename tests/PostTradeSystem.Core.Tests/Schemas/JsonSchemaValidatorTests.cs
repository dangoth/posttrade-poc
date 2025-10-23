using PostTradeSystem.Core.Schemas;
using Xunit;
using FluentAssertions;

namespace PostTradeSystem.Core.Tests.Schemas;

/// <summary>
/// Tests for the JsonSchemaValidator itself. This class tests the validator's
/// core functionality like schema registration, validation logic, and edge cases.
/// </summary>
public class JsonSchemaValidatorTests
{
    [Fact]
    public void RegisterSchema_WithValidSchema_ShouldNotThrow()
    {
        // Arrange
        var validator = new JsonSchemaValidator();
        var validSchema = """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string", "required": true },
                "age": { "type": "number", "required": true }
            }
        }
        """;

        // Act & Assert
        var exception = Record.Exception(() => validator.RegisterSchema("TestSchema", validSchema));
        exception.Should().BeNull("Valid schema registration should not throw");
    }

    [Fact]
    public void RegisterSchema_WithInvalidSchema_ShouldThrow()
    {
        // Arrange
        var validator = new JsonSchemaValidator();
        var invalidSchema = """invalid json schema""";

        // Act & Assert
        var exception = Record.Exception(() => validator.RegisterSchema("TestSchema", invalidSchema));
        exception.Should().NotBeNull("Invalid schema registration should throw");
    }

    [Fact]
    public void ValidateMessage_WithRegisteredSchema_ShouldValidateCorrectly()
    {
        // Arrange
        var validator = new JsonSchemaValidator();
        var schema = """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string", "required": true },
                "age": { "type": "number", "required": true }
            }
        }
        """;
        
        validator.RegisterSchema("Person", schema);

        var validMessage = """{"name": "John", "age": 30}""";
        var invalidMessage = """{"name": "John"}"""; // Missing required age

        // Act
        var validResult = validator.ValidateMessage("Person", validMessage, null);
        var invalidResult = validator.ValidateMessage("Person", invalidMessage, null);

        // Assert
        validResult.Should().BeTrue("Valid message should pass validation");
        invalidResult.Should().BeFalse("Invalid message should fail validation");
    }

    [Fact]
    public void ValidateMessage_WithUnregisteredSchema_ShouldReturnFalse()
    {
        // Arrange
        var validator = new JsonSchemaValidator();
        var message = """{"any": "data"}""";

        // Act - Unregistered schemas with no version return false
        var result = validator.ValidateMessage("UnregisteredSchema", message, null);

        // Assert
        result.Should().BeFalse("Unregistered schemas with no version should return false");
    }

    [Fact]
    public void ValidateMessage_WithUnregisteredSchemaAndVersion_ShouldReturnTrue()
    {
        // Arrange
        var validator = new JsonSchemaValidator();
        var message = """{"any": "data"}""";

        // Act - Unregistered schemas with version return true
        var result = validator.ValidateMessage("UnregisteredSchema", message, 1);

        // Assert
        result.Should().BeTrue("Unregistered schemas with version should return true");
    }

    [Fact]
    public void ValidateMessage_WithNullMessage_ShouldReturnFalse()
    {
        // Arrange
        var validator = new JsonSchemaValidator();
        var schema = """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string", "required": true }
            }
        }
        """;
        
        validator.RegisterSchema("TestSchema", schema);

        // Act
        var result = validator.ValidateMessage("TestSchema", null!, null);

        // Assert
        result.Should().BeFalse("Null message should fail validation");
    }

    [Fact]
    public void ValidateMessage_WithEmptyMessage_ShouldReturnFalse()
    {
        // Arrange
        var validator = new JsonSchemaValidator();
        var schema = """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string", "required": true }
            }
        }
        """;
        
        validator.RegisterSchema("TestSchema", schema);

        // Act
        var result = validator.ValidateMessage("TestSchema", "", null);

        // Assert
        result.Should().BeFalse("Empty message should fail validation");
    }

    [Fact]
    public void ValidateMessage_WithMalformedJson_ShouldReturnFalse()
    {
        // Arrange
        var validator = new JsonSchemaValidator();
        var schema = """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string", "required": true }
            }
        }
        """;
        
        validator.RegisterSchema("TestSchema", schema);
        var malformedJson = """{"name": "John", "invalid": json}""";

        // Act
        var result = validator.ValidateMessage("TestSchema", malformedJson, null);

        // Assert
        result.Should().BeFalse("Malformed JSON should fail validation");
    }

    [Fact]
    public void ValidateMessage_WithVersionParameter_ShouldWork()
    {
        // Arrange
        var validator = new JsonSchemaValidator();
        var schema = """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string", "required": true }
            }
        }
        """;
        
        validator.RegisterSchema("TestSchema", schema);
        var validMessage = """{"name": "John"}""";

        // Act
        var result = validator.ValidateMessage("TestSchema", validMessage, 1);

        // Assert
        result.Should().BeTrue("Valid message with version should pass validation");
    }

    [Fact]
    public void RegisterSchema_SameSchemaNameTwice_ShouldOverwrite()
    {
        // Arrange
        var validator = new JsonSchemaValidator();
        var schema1 = """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string", "required": true }
            }
        }
        """;
        
        var schema2 = """
        {
            "type": "object",
            "properties": {
                "title": { "type": "string", "required": true }
            }
        }
        """;

        // Act
        validator.RegisterSchema("TestSchema", schema1);
        validator.RegisterSchema("TestSchema", schema2); // Should overwrite

        var messageForSchema1 = """{"name": "John"}""";
        var messageForSchema2 = """{"title": "Mr."}""";

        // Assert
        var result1 = validator.ValidateMessage("TestSchema", messageForSchema1, null);
        var result2 = validator.ValidateMessage("TestSchema", messageForSchema2, null);

        result1.Should().BeFalse("Message for old schema should fail after overwrite");
        result2.Should().BeTrue("Message for new schema should pass after overwrite");
    }

    [Fact]
    public void ValidateMessage_WithComplexNestedSchema_ShouldWork()
    {
        // Arrange
        var validator = new JsonSchemaValidator();
        var complexSchema = """
        {
            "type": "object",
            "properties": {
                "person": {
                    "type": "object",
                    "properties": {
                        "name": { "type": "string", "required": true },
                        "address": {
                            "type": "object",
                            "properties": {
                                "street": { "type": "string", "required": true },
                                "city": { "type": "string", "required": true }
                            }
                        }
                    }
                },
                "tags": {
                    "type": "array",
                    "items": { "type": "string" }
                }
            }
        }
        """;
        
        validator.RegisterSchema("ComplexSchema", complexSchema);

        var validComplexMessage = """
        {
            "person": {
                "name": "John",
                "address": {
                    "street": "123 Main St",
                    "city": "Anytown"
                }
            },
            "tags": ["employee", "manager"]
        }
        """;

        var invalidComplexMessage = """
        {
            "person": {
                "name": "John",
                "address": {
                    "street": "123 Main St"
                }
            }
        }
        """; // Missing required city

        // Act
        var validResult = validator.ValidateMessage("ComplexSchema", validComplexMessage, null);
        var invalidResult = validator.ValidateMessage("ComplexSchema", invalidComplexMessage, null);

        // Assert
        validResult.Should().BeTrue("Valid complex message should pass validation");
        invalidResult.Should().BeFalse("Invalid complex message should fail validation");
    }
}