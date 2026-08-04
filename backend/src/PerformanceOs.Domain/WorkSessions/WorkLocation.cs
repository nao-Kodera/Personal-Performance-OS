namespace PerformanceOs.Domain.WorkSessions;

/// <summary>
/// 作業場所（docs/02-glossary.md §3.1）。
/// </summary>
/// <remarks>
/// MVP の分析 6 種には場所別の集計を含めない。初期段階では自宅に偏り、
/// 区分間の比較が成立しないためである。ただしデータは保存する。
/// 「保存するが集計しない」であって「集計したいが保存していない」ではない
/// （docs/04-analytics-spec.md §3.3）。
/// </remarks>
public enum WorkLocation
{
    Home,
    Office,
    Cafe,

    /// <summary>その他。この値のときのみ補足テキストを設定できる（WC-2）。</summary>
    Other,
}
