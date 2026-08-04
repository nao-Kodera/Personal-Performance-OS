using PerformanceOs.Domain.Time;

namespace PerformanceOs.Domain.Tests.Time;

/// <summary>
/// 時刻を固定するテスト用の <see cref="IClock"/>。
/// </summary>
/// <remarks>
/// モックライブラリを使わず、この実装を書く方針による
/// （docs/08-technical-design.md §6.1）。
/// </remarks>
internal sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; }

    public DateOnly TodayJst => JstCalendar.ToJstDate(UtcNow);
}
