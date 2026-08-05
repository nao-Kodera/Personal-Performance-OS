using PerformanceOs.Domain.Common;
using PerformanceOs.Domain.Time;

namespace PerformanceOs.Domain.ValueObjects;

/// <summary>
/// 作業セッションの期間。開始時刻と終了時刻から、所属日・時間帯・実作業時間を導出する。
/// </summary>
/// <remarks>
/// <para>
/// JST 変換を <see cref="JstCalendar"/> に委譲し、日付境界の解釈を 1 箇所に保つ
/// （docs/05-domain-design.md §5.3）。
/// </para>
/// <para>
/// <b>所属日は開始時刻のみで決まる。</b>22:00 開始・翌 01:00 終了のセッションは
/// 開始日側に集計され、終了日側には含めない（docs/02-glossary.md §4）。
/// </para>
/// </remarks>
public readonly record struct SessionPeriod
{
    public SessionPeriod(DateTimeOffset startedAt, DateTimeOffset? finishedAt)
    {
        // WS-5
        if (finishedAt is not null && finishedAt <= startedAt)
        {
            throw new DomainException(
                $"終了時刻は開始時刻より後である必要があります: 開始 {startedAt:O} / 終了 {finishedAt:O}");
        }

        StartedAt = startedAt;
        FinishedAt = finishedAt;
    }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? FinishedAt { get; }

    /// <summary>未終了なら null。</summary>
    public TimeSpan? Duration => FinishedAt - StartedAt;

    /// <summary>
    /// 実作業時間（分）。中断時間は差し引かない（docs/02-glossary.md §5）。
    /// </summary>
    public int? DurationMinutes => Duration is { } duration ? (int)duration.TotalMinutes : null;

    /// <summary>このセッションが集計される JST 基準の日付。</summary>
    public DateOnly BelongingDate => JstCalendar.ToJstDate(StartedAt);

    /// <summary>分析 A-03 で使う時間帯区分。開始時刻から決まる。</summary>
    public TimeBand TimeBand => JstCalendar.ToTimeBand(StartedAt);
}
