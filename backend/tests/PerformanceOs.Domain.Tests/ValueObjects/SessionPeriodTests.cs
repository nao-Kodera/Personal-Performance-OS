using PerformanceOs.Domain.Common;
using PerformanceOs.Domain.Time;
using PerformanceOs.Domain.ValueObjects;

namespace PerformanceOs.Domain.Tests.ValueObjects;

public class SessionPeriodTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 8, 4, 0, 12, 0, TimeSpan.Zero);

    [Fact]
    public void 未終了なら実作業時間を持たない()
    {
        var period = new SessionPeriod(StartedAt, null);

        Assert.Null(period.Duration);
        Assert.Null(period.DurationMinutes);
    }

    [Fact]
    public void 実作業時間は分単位で切り捨てられる()
    {
        // 93 分 30 秒
        var period = new SessionPeriod(StartedAt, StartedAt.AddMinutes(93).AddSeconds(30));

        Assert.Equal(93, period.DurationMinutes);
    }

    /// <summary>WS-5</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void 終了時刻が開始時刻以前なら例外になる(int offsetSeconds)
    {
        Assert.Throws<DomainException>(
            () => new SessionPeriod(StartedAt, StartedAt.AddSeconds(offsetSeconds)));
    }

    [Fact]
    public void 一秒でも後なら成立する()
    {
        var period = new SessionPeriod(StartedAt, StartedAt.AddSeconds(1));

        Assert.Equal(0, period.DurationMinutes);
    }

    /// <summary>
    /// 所属日・時間帯はいずれも開始時刻から決まり、終了時刻は関与しない
    /// （docs/02-glossary.md §4）。
    /// </summary>
    [Fact]
    public void 所属日と時間帯は終了時刻に影響されない()
    {
        // UTC 13:00 = JST 22:00、終了は JST 翌 01:00
        var startedAt = new DateTimeOffset(2026, 8, 4, 13, 0, 0, TimeSpan.Zero);
        var withoutFinish = new SessionPeriod(startedAt, null);
        var withFinish = new SessionPeriod(startedAt, new DateTimeOffset(2026, 8, 4, 16, 0, 0, TimeSpan.Zero));

        Assert.Equal(withoutFinish.BelongingDate, withFinish.BelongingDate);
        Assert.Equal(withoutFinish.TimeBand, withFinish.TimeBand);
        Assert.Equal(new DateOnly(2026, 8, 4), withFinish.BelongingDate);
        Assert.Equal(TimeBand.Evening, withFinish.TimeBand);
    }

    [Fact]
    public void 日をまたぐ長時間セッションでも所属日は開始日()
    {
        // JST 2026-08-04 09:00 開始、30 時間後に終了
        var startedAt = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
        var period = new SessionPeriod(startedAt, startedAt.AddHours(30));

        Assert.Equal(new DateOnly(2026, 8, 4), period.BelongingDate);
        Assert.Equal(1800, period.DurationMinutes);
    }
}
