namespace PostTradeSystem.Infrastructure.Repositories;

public class MetadataParsingResult
{
    public int SchemaVersion { get; init; }
    public bool IsReliable { get; init; }
    public string? WarningMessage { get; init; }
    public MetadataParsingStrategy Strategy { get; init; }
    public bool ShouldDeadLetter { get; init; }
    public string? DeadLetterReason { get; init; }

    public static MetadataParsingResult Success(int version, MetadataParsingStrategy strategy)
        => new() { SchemaVersion = version, IsReliable = true, Strategy = strategy };

    public static MetadataParsingResult Warning(int version, MetadataParsingStrategy strategy, string warning)
        => new() { SchemaVersion = version, IsReliable = false, Strategy = strategy, WarningMessage = warning };

    public static MetadataParsingResult DeadLetter(string reason)
        => new() { ShouldDeadLetter = true, DeadLetterReason = reason, Strategy = MetadataParsingStrategy.DeadLetter };
}

public enum MetadataParsingStrategy
{
    ExplicitVersion,      // Found valid SchemaVersion in metadata
    InferredFromData,     // Inferred from event data structure  
    HistoricalFallback,   // Based on event creation date
    DeadLetter           // Cannot determine - move to DLQ
}