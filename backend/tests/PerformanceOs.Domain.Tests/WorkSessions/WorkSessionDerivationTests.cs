using PerformanceOs.Domain.Time;

namespace PerformanceOs.Domain.Tests.WorkSessions;

/// <summary>
/// 導出値。いずれも保存せず、元データから都度算出する
/// （docs/04-analytics-spec.md §5）。
/// </summary>
public class WorkSessionDerivationTests
{
    [Fact]
    public void 進行中は実作業時間を持たない()
    {
        var session = WorkSessionFactory.Started();

        Assert.Null(session.DurationMinutes);
    }

    [Fact]
    public void 実作業時間は開始から終了までの分数()
    {
        // UTC 00:12 → 01:45
        var session = WorkSessionFactory.Completed();

        Assert.Equal(93, session.DurationMinutes);
    }

    [Fact]
    public void 中断終了でも実作業時間を持つ()
    {
        var session = WorkSessionFactory.Abandoned();

        Assert.Equal(93, session.DurationMinutes);
    }

    /// <summary>
    /// 所属日は開始時刻の JST 日付。UTC 00:12 は JST 09:12 で同日。
    /// </summary>
    [Fact]
    public void 所属日はJST基準の開始日()
    {
        var session = WorkSessionFactory.Started();

        Assert.Equal(new DateOnly(2026, 8, 4), session.BelongingDate);
    }

    /// <summary>
    /// docs/08-technical-design.md §6.3 T-20。
    /// JST 22:00 開始・翌 01:00 終了でも、所属日は開始日側になる。
    /// </summary>
    [Fact]
    public void 深夜をまたぐセッションの所属日は開始日になる()
    {
        // UTC 13:00 = JST 22:00
        var startedAt = new DateTimeOffset(2026, 8, 4, 13, 0, 0, TimeSpan.Zero);
        var session = WorkSessionFactory.Started(now: startedAt);

        // UTC 16:00 = JST 翌 01:00
        session.Finish(0, WorkSessionFactory.Result(), new DateTimeOffset(2026, 8, 4, 16, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 8, 4), session.BelongingDate);
        Assert.Equal(180, session.DurationMinutes);
    }

    [Fact]
    public void 時間帯区分は開始時刻から決まる()
    {
        // UTC 00:12 = JST 09:12
        var session = WorkSessionFactory.Started();

        Assert.Equal(TimeBand.Morning, session.TimeBand);
    }

    [Fact]
    public void 深夜開始の時間帯区分は夜になる()
    {
        // UTC 13:00 = JST 22:00
        var session = WorkSessionFactory.Started(now: new DateTimeOffset(2026, 8, 4, 13, 0, 0, TimeSpan.Zero));

        Assert.Equal(TimeBand.Evening, session.TimeBand);
    }

    // ------------------------------------------------------------------
    // 疲労増加量・見込みとの差
    // ------------------------------------------------------------------

    [Fact]
    public void 未評価なら疲労増加量と見込み差を持たない()
    {
        var session = WorkSessionFactory.Started();

        Assert.Null(session.FatigueDelta);
        Assert.Null(session.FocusGap);
    }

    [Fact]
    public void 中断終了では疲労増加量を持たない()
    {
        var session = WorkSessionFactory.Abandoned();

        Assert.Null(session.FatigueDelta);
    }

    [Theory]
    [InlineData(2, 4, 2)]    // 疲労 2 → 4
    [InlineData(5, 1, -4)]   // 下限
    [InlineData(1, 5, 4)]    // 上限
    [InlineData(3, 3, 0)]
    public void 疲労増加量は終了時と作業前の差(int before, int after, int expected)
    {
        var session = WorkSessionFactory.Started(
            preWorkState: WorkSessionFactory.PreWorkState(fatigue: before));
        session.Finish(0, WorkSessionFactory.Result(fatigueAfter: after), WorkSessionFactory.FinishedAt);

        Assert.Equal(expected, session.FatigueDelta);
    }

    /// <summary>
    /// 見込みとの差が常に負なら、自分の状態認識が楽観的すぎることを意味する
    /// （docs/04-analytics-spec.md §5）。
    /// </summary>
    [Theory]
    [InlineData(4, 4, 0)]
    [InlineData(4, 2, -2)]   // 見込みより集中できなかった
    [InlineData(2, 5, 3)]
    public void 見込み差は実際の集中度と見込みの差(int expectedFocus, int actualFocus, int expected)
    {
        var session = WorkSessionFactory.Started(
            preWorkState: WorkSessionFactory.PreWorkState(expectedFocus: expectedFocus));
        session.Finish(0, WorkSessionFactory.Result(focus: actualFocus), WorkSessionFactory.FinishedAt);

        Assert.Equal(expected, session.FocusGap);
    }

    [Fact]
    public void 予定外のセッションは予定を参照しない()
    {
        Assert.Null(WorkSessionFactory.Started().PlannedWorkId);
    }

    [Fact]
    public void 予定から開始したセッションは予定を参照する()
    {
        Assert.Equal(88, WorkSessionFactory.Started(plannedWorkId: 88).PlannedWorkId);
    }
}
