using PerformanceOs.Domain.Time;

namespace PerformanceOs.Application.Tests.Fakes;

/// <summary>時刻を固定するテスト用の <see cref="IClock"/>。</summary>
internal sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; set; }

    public DateOnly TodayJst => JstCalendar.ToJstDate(UtcNow);
}
