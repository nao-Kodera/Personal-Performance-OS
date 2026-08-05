using PerformanceOs.Domain.Common;
using PerformanceOs.Domain.Time;
using PerformanceOs.Domain.ValueObjects;

namespace PerformanceOs.Domain.Tests.ValueObjects;

public class SleepDurationTests
{
    [Theory]
    [InlineData(15)]
    [InlineData(435)]
    [InlineData(1440)]
    public void 有効な値で生成できる(int minutes)
    {
        var duration = new SleepDuration(minutes);

        Assert.Equal(minutes, duration.Minutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    [InlineData(1455)]
    public void 範囲外の値は例外になる(int minutes)
    {
        Assert.Throws<DomainException>(() => new SleepDuration(minutes));
    }

    [Theory]
    [InlineData(20)]
    [InlineData(100)]
    [InlineData(431)]
    [InlineData(1439)]
    public void 十五分単位でない値は例外になる(int minutes)
    {
        Assert.Throws<DomainException>(() => new SleepDuration(minutes));
    }

    /// <summary>
    /// docs/04-analytics-spec.md A-05 の区分境界。
    /// 境界値 360 / 420 / 480 はいずれも 15 の倍数であり、正確に指定できる。
    /// </summary>
    [Theory]
    [InlineData(15, SleepBand.Under6)]
    [InlineData(345, SleepBand.Under6)]    // 5:45
    [InlineData(360, SleepBand.From6To7)]  // 6:00 ちょうど
    [InlineData(405, SleepBand.From6To7)]  // 6:45
    [InlineData(420, SleepBand.From7To8)]  // 7:00 ちょうど
    [InlineData(465, SleepBand.From7To8)]  // 7:45
    [InlineData(480, SleepBand.Over8)]     // 8:00 ちょうど
    [InlineData(1440, SleepBand.Over8)]
    public void 睡眠時間区分を導出できる(int minutes, SleepBand expected)
    {
        Assert.Equal(expected, new SleepDuration(minutes).ToBand());
    }

    [Theory]
    [InlineData(435, "7:15")]
    [InlineData(360, "6:00")]
    [InlineData(1440, "24:00")]
    public void 文字列表現は時分形式(int minutes, string expected)
    {
        Assert.Equal(expected, new SleepDuration(minutes).ToString());
    }
}
