namespace PerformanceOs.Application.WorkSessions;

/// <summary>
/// 保存は妨げないが、利用者に提示する情報（docs/07-api-design.md §2.15）。
/// </summary>
/// <remarks>
/// これらは<b>エラーではない</b>。8 時間を超える作業も 1 分未満の作業も
/// 実際に起こりうる。記録を拒否すると事実が失われるため、警告に留める。
/// </remarks>
public enum SessionWarning
{
    /// <summary>実作業時間が 8 時間を超えている。記録し忘れの可能性がある。</summary>
    LongSession,

    /// <summary>実作業時間が 1 分未満。中断終了の方が適切かもしれない。</summary>
    VeryShortSession,

    /// <summary>
    /// 所属日の日次コンディションが未記録。睡眠に関する分析（A-05）から
    /// 除外される。T-20 以降で判定する。
    /// </summary>
    MissingDailyCondition,
}
