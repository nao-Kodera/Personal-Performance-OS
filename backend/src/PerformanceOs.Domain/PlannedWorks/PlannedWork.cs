using PerformanceOs.Domain.Common;
using PerformanceOs.Domain.Time;

namespace PerformanceOs.Domain.PlannedWorks;

/// <summary>
/// 「このタスクを、この日に、この作業タイプでやる」という事前の宣言。
/// </summary>
/// <remarks>
/// <para>
/// <b>目的は実行率の分母を作ることである</b>（docs/02-glossary.md §1）。
/// TaskItem に予定日を持たせない理由もそこにある。1 タスク 1 予定に制限されると
/// 実行率の意味が変わる（docs/05-domain-design.md §4.4）。
/// </para>
/// <para>
/// <b>同一タスク・同一日の重複を許す（PW-3）。</b>同じタスクを 1 日に複数回
/// 予定するのは正常な操作である。一意制約を置かないこと。
/// </para>
/// <para>
/// <b>PW-1（当日のみ）・PW-4 / PW-5（実行と未実行の排他）はここで検査しない。</b>
/// PW-1 は現在時刻を、PW-4 / PW-5 は別集約（WorkSession）の情報を必要とし、
/// 集約内で判定できない。アプリケーション層が担う
/// （docs/05-domain-design.md §4.4 / §6）。
/// </para>
/// </remarks>
public sealed class PlannedWork : Entity
{
    /// <summary>予定所要時間の下限・上限・単位（PW-2）。</summary>
    public const int MinPlannedMinutes = 15;
    public const int MaxPlannedMinutes = 1440;
    public const int PlannedMinutesUnit = 15;

    /// <summary>EF Core 用。</summary>
    private PlannedWork()
    {
    }

    private PlannedWork(
        DateOnly targetDate,
        long taskItemId,
        long workTypeId,
        TimeBand? plannedTimeBand,
        int? plannedMinutes,
        DateTimeOffset createdAt)
    {
        TargetDate = targetDate;
        TaskItemId = taskItemId;
        WorkTypeId = workTypeId;
        PlannedTimeBand = ValidateTimeBand(plannedTimeBand);
        PlannedMinutes = ValidateMinutes(plannedMinutes);
        CreatedAt = createdAt;
    }

    /// <summary>対象日（JST 基準の論理日）。</summary>
    public DateOnly TargetDate { get; private set; }

    public long TaskItemId { get; private set; }

    public long WorkTypeId { get; private set; }

    /// <summary>予定した時間帯。任意。</summary>
    public TimeBand? PlannedTimeBand { get; private set; }

    /// <summary>予定所要時間（分）。任意。指定する場合は 15 分単位（PW-2）。</summary>
    public int? PlannedMinutes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>未実行の記録。実行された、または未処理なら null。</summary>
    public NonExecutionRecord? NonExecution { get; private set; }

    /// <summary>未実行として処理済みか。</summary>
    public bool IsUnexecuted => NonExecution is not null;

    /// <summary>
    /// 予定を立てる。
    /// </summary>
    /// <remarks>
    /// <paramref name="targetDate"/> が当日であることの確認は呼び出し側で行う（PW-1）。
    /// </remarks>
    public static PlannedWork Plan(
        DateOnly targetDate,
        long taskItemId,
        long workTypeId,
        TimeBand? plannedTimeBand,
        int? plannedMinutes,
        DateTimeOffset createdAt)
        => new(targetDate, taskItemId, workTypeId, plannedTimeBand, plannedMinutes, createdAt);

    /// <summary>
    /// 実行しなかったことを記録する。既に記録がある場合は訂正する（NE-1）。
    /// </summary>
    /// <remarks>
    /// <b>WorkSession が紐づいていないことの確認は呼び出し側で行う（PW-5）。</b>
    /// 別集約の情報であり、ここでは判定できない。
    /// </remarks>
    public void RecordNonExecution(NonExecutionReason reason, string? note, DateTimeOffset now)
    {
        if (NonExecution is null)
        {
            NonExecution = new NonExecutionRecord(reason, note, now);
        }
        else
        {
            NonExecution.Update(reason, note, now);
        }
    }

    private static TimeBand? ValidateTimeBand(TimeBand? timeBand)
    {
        if (timeBand is { } value && !Enum.IsDefined(value))
        {
            throw new DomainException($"予定時間帯が不正です: {value}");
        }

        return timeBand;
    }

    private static int? ValidateMinutes(int? minutes)
    {
        if (minutes is not { } value)
        {
            return null;
        }

        if (value is < MinPlannedMinutes or > MaxPlannedMinutes)
        {
            throw new DomainException(
                $"予定所要時間は {MinPlannedMinutes}〜{MaxPlannedMinutes} 分の範囲である必要があります: {value}");
        }

        if (value % PlannedMinutesUnit != 0)
        {
            throw new DomainException(
                $"予定所要時間は {PlannedMinutesUnit} 分単位である必要があります: {value}");
        }

        return value;
    }
}
