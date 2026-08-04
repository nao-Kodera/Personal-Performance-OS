using PerformanceOs.Domain.Time;

namespace PerformanceOs.Infrastructure.Time;

/// <summary>
/// システム時刻を返す <see cref="IClock"/> の実装。
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly TodayJst => JstCalendar.ToJstDate(UtcNow);
}
