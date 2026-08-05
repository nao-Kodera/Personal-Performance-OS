using System.Reflection;
using PerformanceOs.Domain.Common;
using PerformanceOs.Domain.DailyConditions;
using PerformanceOs.Domain.Time;
using PerformanceOs.Domain.ValueObjects;

namespace PerformanceOs.Domain.Tests.DailyConditions;

public class DailyConditionTests
{
    private static readonly DateOnly Today = new(2026, 8, 5);
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 0, 0, 0, TimeSpan.Zero);

    private static DailyConditionInput Input(
        int sleepMinutes = 435, int physical = 4, int mood = 4, int stress = 2, string? note = null)
        => new(new SleepDuration(sleepMinutes), new Rating(physical), new Rating(mood),
            new Rating(stress), note);

    private static DailyCondition Record(int sleepMinutes = 435)
        => DailyCondition.Record(Today, Input(sleepMinutes), Now);

    [Fact]
    public void 記録した値を保持する()
    {
        var condition = DailyCondition.Record(Today, Input(note: "  よく眠れた  "), Now);

        Assert.Equal(Today, condition.TargetDate);
        Assert.Equal(435, condition.Sleep.Minutes);
        Assert.Equal(4, condition.PhysicalCondition.Value);
        Assert.Equal(2, condition.StressLevel.Value);
        Assert.Equal("よく眠れた", condition.Note);
        Assert.Equal(Now, condition.RecordedAt);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(1455)]
    [InlineData(100)]
    public void 睡眠時間が範囲外または15分単位でなければ拒む(int minutes)
    {
        Assert.Throws<DomainException>(() => new SleepDuration(minutes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void 評価が1から5の範囲外なら拒む(int value)
    {
        Assert.Throws<DomainException>(() => new Rating(value));
    }

    [Fact]
    public void メモが上限を超えたら拒む()
    {
        var tooLong = new string('あ', DailyCondition.MaxNoteLength + 1);

        Assert.Throws<DomainException>(
            () => DailyCondition.Record(Today, Input(note: tooLong), Now));
    }

    [Fact]
    public void 空白だけのメモはnullにする()
    {
        var condition = DailyCondition.Record(Today, Input(note: "   "), Now);

        Assert.Null(condition.Note);
    }

    /// <summary>
    /// DC-5。RecordedAt と UpdatedAt の差で、事後に訂正されたことが分かる。
    /// </summary>
    [Fact]
    public void 訂正しても初回記録時刻は変わらない()
    {
        var condition = Record();
        var later = Now.AddHours(3);

        condition.Update(Input(sleepMinutes: 420, physical: 3), later);

        Assert.Equal(Now, condition.RecordedAt);
        Assert.Equal(later, condition.UpdatedAt);
        Assert.Equal(420, condition.Sleep.Minutes);
        Assert.Equal(3, condition.PhysicalCondition.Value);
    }

    /// <summary>
    /// DC-4 を迂回されないための確認。
    /// </summary>
    /// <remarks>
    /// 対象日を後から移せると、当日として記録したものを過去日に付け替えられる。
    /// 「当日のみ記録可」の判定はアプリケーション層にあるため、ここを塞いでおかないと
    /// 制約そのものが意味を失う（docs/05-domain-design.md §4.3）。
    /// </remarks>
    [Fact]
    public void 対象日を変更する手段を持たない()
    {
        var targetDate = typeof(DailyCondition).GetProperty(nameof(DailyCondition.TargetDate));

        Assert.NotNull(targetDate);
        Assert.False(targetDate.SetMethod is { IsPublic: true });

        var mutators = typeof(DailyCondition)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();

        // 状態を変える公開メソッドは Update だけである。
        Assert.Equal(["Update"], mutators);
    }

    /// <summary>
    /// 区分の境界そのものが分析 A-05 の定義である（docs/02-glossary.md §3.3）。
    /// </summary>
    [Theory]
    [InlineData(345, SleepBand.Under6)]   // 5:45
    [InlineData(360, SleepBand.From6To7)] // 6:00
    [InlineData(405, SleepBand.From6To7)] // 6:45
    [InlineData(420, SleepBand.From7To8)] // 7:00
    [InlineData(465, SleepBand.From7To8)] // 7:45
    [InlineData(480, SleepBand.Over8)]    // 8:00
    [InlineData(600, SleepBand.Over8)]    // 10:00
    public void 睡眠時間区分を境界で正しく導出する(int minutes, SleepBand expected)
    {
        Assert.Equal(expected, Record(minutes).SleepBand);
    }
}
