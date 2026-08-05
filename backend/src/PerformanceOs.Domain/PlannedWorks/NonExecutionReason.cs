namespace PerformanceOs.Domain.PlannedWorks;

/// <summary>
/// 予定を実行しなかった理由（docs/02-glossary.md §1）。
/// </summary>
/// <remarks>
/// <b>理由を必須にしている。</b>理由なしの未実行記録は実行率の分子にはなるが、
/// 「計画が妥当だったか」の検証には使えない（docs/01-product-requirements.md §2 P4）。
/// <para>
/// <see cref="Overplanned"/> を他から分離していることが重要である。これが多い
/// 場合、対処は「頑張る」ではなく「計画を減らす」になる。
/// </para>
/// </remarks>
public enum NonExecutionReason
{
    /// <summary>時間がなかった。他の作業・予定に時間を取られた。</summary>
    NoTime,

    /// <summary>割り込みが入った。突発的な依頼・対応が発生した。</summary>
    Interrupted,

    /// <summary>体調・集中が足りなかった。</summary>
    PoorCondition,

    /// <summary>優先度が下がった。実行する必要がなくなった。</summary>
    Deprioritized,

    /// <summary>計画が過大だった。そもそも実行不可能な量を計画していた。</summary>
    Overplanned,

    Other,
}
