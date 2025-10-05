namespace PostTradeSystem.Core.Services;

public interface ITimeProvider
{
    DateTime UtcNow { get; }
    DateTimeOffset UtcNowOffset { get; }
}

public class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTimeOffset UtcNowOffset => DateTimeOffset.UtcNow;
}