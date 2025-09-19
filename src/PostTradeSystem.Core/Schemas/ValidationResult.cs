namespace PostTradeSystem.Core.Schemas;

public record ValidationResult(bool IsValid, string? ErrorMessage)
{
    public static ValidationResult Success() => new(true, null);
    public static ValidationResult Failure(string errorMessage) => new(false, errorMessage);
}