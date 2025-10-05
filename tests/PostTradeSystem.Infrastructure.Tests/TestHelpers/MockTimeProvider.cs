using PostTradeSystem.Core.Services;

namespace PostTradeSystem.Infrastructure.Tests.TestHelpers;

public class MockTimeProvider : ITimeProvider
{
    private DateTime _currentTime = DateTime.UtcNow;

    public DateTime UtcNow => _currentTime;
    public DateTimeOffset UtcNowOffset => new DateTimeOffset(_currentTime);

    public void SetTime(DateTime time)
    {
        _currentTime = time;
    }

    public void AdvanceTime(TimeSpan timeSpan)
    {
        _currentTime = _currentTime.Add(timeSpan);
    }

    public void AdvanceMinutes(int minutes)
    {
        AdvanceTime(TimeSpan.FromMinutes(minutes));
    }

    public void AdvanceHours(int hours)
    {
        AdvanceTime(TimeSpan.FromHours(hours));
    }
}