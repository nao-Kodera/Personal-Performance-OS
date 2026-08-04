namespace PerformanceOs.Domain.Time;

/// <summary>
/// 睡眠時間区分。DailyCondition の睡眠時間から導出する（docs/02-glossary.md §3.3）。
/// </summary>
/// <remarks>
/// この値は保存しない。睡眠時間（分）から都度導出する。
/// 宣言順が分析画面の表示順（睡眠時間の昇順）と一致している
/// （docs/04-analytics-spec.md A-05）。
/// </remarks>
public enum SleepBand
{
    /// <summary>6 時間未満</summary>
    Under6,

    /// <summary>6 時間以上 7 時間未満</summary>
    From6To7,

    /// <summary>7 時間以上 8 時間未満</summary>
    From7To8,

    /// <summary>8 時間以上</summary>
    Over8,
}
