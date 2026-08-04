namespace PerformanceOs.Domain.WorkSessions;

/// <summary>
/// 作業セッションの状態（docs/02-glossary.md §1）。
/// </summary>
public enum SessionStatus
{
    /// <summary>進行中。開始済み、未終了。PerformanceResult を持たない。</summary>
    InProgress,

    /// <summary>
    /// 完了。正常に終了し、成果を評価した。PerformanceResult を<b>必ず</b>持つ（WS-3）。
    /// 分析の母集団はこの状態のみ（docs/04-analytics-spec.md §2.1）。
    /// </summary>
    Completed,

    /// <summary>
    /// 中断終了。開始したが作業として成立せず終了した。PerformanceResult を持たない。
    /// </summary>
    /// <remarks>
    /// 分析 A-01〜A-05 の母集団からは除外するが、記録としては保持する。
    /// ただし実行率 A-06 では「着手した」として実行済みに数える。
    /// この非対称は意図的である（docs/04-analytics-spec.md A-06）。
    /// </remarks>
    Abandoned,
}
