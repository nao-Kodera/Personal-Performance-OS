using PerformanceOs.Domain.Common;
using PerformanceOs.Domain.Time;

namespace PerformanceOs.Domain.ValueObjects;

/// <summary>
/// 睡眠時間。15 分単位で記録する（docs/02-glossary.md §3.3）。
/// </summary>
/// <remarks>
/// 15 分単位に制限しているのは、自由入力による誤入力と入力時間の増加を防ぐため。
/// 日次コンディションの入力は 30 秒以内に収める必要がある
/// （docs/01-product-requirements.md §8 原則 1）。
/// </remarks>
public readonly record struct SleepDuration
{
    public const int MinMinutes = 15;
    public const int MaxMinutes = 1440;
    public const int UnitMinutes = 15;

    private const int SixHours = 6 * 60;
    private const int SevenHours = 7 * 60;
    private const int EightHours = 8 * 60;

    public int Minutes { get; }

    public SleepDuration(int minutes)
    {
        if (minutes is < MinMinutes or > MaxMinutes)
        {
            throw new DomainException(
                $"睡眠時間は {MinMinutes}〜{MaxMinutes} 分の範囲である必要があります: {minutes}");
        }

        if (minutes % UnitMinutes != 0)
        {
            throw new DomainException(
                $"睡眠時間は {UnitMinutes} 分単位である必要があります: {minutes}");
        }

        Minutes = minutes;
    }

    /// <summary>
    /// 分析 A-05 で使う睡眠時間区分を返す（docs/04-analytics-spec.md A-05）。
    /// </summary>
    public SleepBand ToBand() => Minutes switch
    {
        < SixHours => SleepBand.Under6,
        < SevenHours => SleepBand.From6To7,
        < EightHours => SleepBand.From7To8,
        _ => SleepBand.Over8,
    };

    public override string ToString() => $"{Minutes / 60}:{Minutes % 60:00}";
}
