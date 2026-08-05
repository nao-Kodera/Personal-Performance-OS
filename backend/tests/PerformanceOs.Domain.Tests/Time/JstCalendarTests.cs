using PerformanceOs.Domain.Time;

namespace PerformanceOs.Domain.Tests.Time;

public class JstCalendarTests
{
    private static readonly TimeSpan JstOffset = TimeSpan.FromHours(9);

    private static DateTimeOffset Jst(int year, int month, int day, int hour, int minute)
        => new(year, month, day, hour, minute, 0, JstOffset);

    // ------------------------------------------------------------------
    // ToTimeBand
    // docs/08-technical-design.md §6.3 T-21 / T-22 / T-23
    // Evening が 17:00–04:59 と日をまたぐため、境界を厚く取る。
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0, TimeBand.Evening)]
    [InlineData(4, 59, TimeBand.Evening)]        // T-21
    [InlineData(5, 0, TimeBand.EarlyMorning)]    // T-22
    [InlineData(8, 59, TimeBand.EarlyMorning)]
    [InlineData(9, 0, TimeBand.Morning)]
    [InlineData(11, 59, TimeBand.Morning)]
    [InlineData(12, 0, TimeBand.Afternoon)]
    [InlineData(16, 59, TimeBand.Afternoon)]     // T-23
    [InlineData(17, 0, TimeBand.Evening)]        // T-23
    [InlineData(23, 59, TimeBand.Evening)]
    public void 時間帯区分を導出できる(int jstHour, int jstMinute, TimeBand expected)
    {
        var instant = Jst(2026, 8, 4, jstHour, jstMinute);

        Assert.Equal(expected, JstCalendar.ToTimeBand(instant));
    }

    /// <summary>
    /// UTC で表現された時刻でも、JST に変換した上で判定されること。
    /// オフセット付き値の時刻部分をそのまま読んでいないことの確認。
    /// </summary>
    [Fact]
    public void UTC表現の時刻もJSTに変換して判定される()
    {
        // UTC 2026-08-03 20:00 = JST 2026-08-04 05:00
        var utc = new DateTimeOffset(2026, 8, 3, 20, 0, 0, TimeSpan.Zero);

        Assert.Equal(TimeBand.EarlyMorning, JstCalendar.ToTimeBand(utc));
    }

    [Fact]
    public void 全ての時刻がいずれかの区分に割り当てられる()
    {
        for (var hour = 0; hour < 24; hour++)
        {
            var band = JstCalendar.ToTimeBand(Jst(2026, 8, 4, hour, 30));

            Assert.True(Enum.IsDefined(band), $"{hour} 時が未定義の区分になった");
        }
    }

    // ------------------------------------------------------------------
    // ToJstDate
    // ------------------------------------------------------------------

    [Fact]
    public void JSTの日付境界の直前は前日になる()
    {
        // UTC 2026-08-03 14:59 = JST 2026-08-03 23:59
        var utc = new DateTimeOffset(2026, 8, 3, 14, 59, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 8, 3), JstCalendar.ToJstDate(utc));
    }

    [Fact]
    public void JSTの日付境界ちょうどは新しい日になる()
    {
        // UTC 2026-08-03 15:00 = JST 2026-08-04 00:00
        var utc = new DateTimeOffset(2026, 8, 3, 15, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 8, 4), JstCalendar.ToJstDate(utc));
    }

    /// <summary>
    /// docs/08-technical-design.md §6.3 T-20。
    /// 22:00 開始・翌 01:00 終了のセッションは、開始日側に集計される。
    /// 所属日は開始時刻のみで決まり、終了時刻は関与しない
    /// （docs/02-glossary.md §4）。
    /// </summary>
    [Fact]
    public void 深夜をまたぐセッションの所属日は開始日になる()
    {
        var startedAt = Jst(2026, 8, 4, 22, 0);
        var finishedAt = Jst(2026, 8, 5, 1, 0);

        var belongingDate = JstCalendar.ToJstDate(startedAt);

        Assert.Equal(new DateOnly(2026, 8, 4), belongingDate);
        Assert.NotEqual(belongingDate, JstCalendar.ToJstDate(finishedAt));
    }

    // ------------------------------------------------------------------
    // IClock.TodayJst
    // docs/08-technical-design.md §6.3 T-24
    // ------------------------------------------------------------------

    [Fact]
    public void 深夜零時過ぎのTodayJstはUTCの日付と異なる()
    {
        // UTC 2026-08-03 15:15 = JST 2026-08-04 00:15
        var utcNow = new DateTimeOffset(2026, 8, 3, 15, 15, 0, TimeSpan.Zero);
        var clock = new FixedClock(utcNow);

        Assert.Equal(new DateOnly(2026, 8, 4), clock.TodayJst);
        Assert.Equal(new DateOnly(2026, 8, 3), DateOnly.FromDateTime(utcNow.UtcDateTime));
    }

    [Fact]
    public void 日中のTodayJstはUTCの日付と一致する()
    {
        // UTC 2026-08-04 03:00 = JST 2026-08-04 12:00
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 4, 3, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 8, 4), clock.TodayJst);
    }

    // ------------------------------------------------------------------
    // 夏時間
    // ------------------------------------------------------------------

    /// <summary>
    /// JST は夏時間を持たない。年間を通じて UTC+9 であること。
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    public void JSTは季節によらずUTCプラス九時間(int month)
    {
        // UTC 15:00 は常に翌日 00:00 (JST)
        var utc = new DateTimeOffset(2026, month, 15, 15, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, month, 16), JstCalendar.ToJstDate(utc));
    }
}
