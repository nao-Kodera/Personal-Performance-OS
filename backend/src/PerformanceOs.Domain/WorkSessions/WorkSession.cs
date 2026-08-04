using PerformanceOs.Domain.Common;
using PerformanceOs.Domain.Time;
using PerformanceOs.Domain.ValueObjects;

namespace PerformanceOs.Domain.WorkSessions;

/// <summary>
/// 実際に行われた 1 回の作業。本プロダクトの中心となる記録であり、集約ルートである。
/// </summary>
/// <remarks>
/// <para>
/// この集約は「開始前の状態」「作業中の事実」「作業後の結果」を分離して保持する。
/// 統合すると、記録タイミングの区別・説明変数と目的変数の独立性・
/// 部分的な状態の表現・編集権限の分離がすべて失われる
/// （docs/05-domain-design.md §1）。
/// </para>
/// <para>
/// 状態遷移:
/// <code>
///                InProgress
///                 /       \
///          Finish()       Abandon()
///             /               \
///       Completed          Abandoned
///        （終端）             （終端）
/// </code>
/// 終端状態から他の状態へ遷移しない（WS-7）。
/// </para>
/// <para>
/// <b>同時に InProgress のセッションは全体で 1 件までである（WS-9）。</b>
/// これは集約をまたぐグローバル制約のため、ここでは担保できない。
/// アプリケーション層と DB の部分一意インデックス
/// <c>uq_work_sessions_single_active</c> で二重に担保する。
/// 並行作業を記録すると、どちらの成果か分離できないためこの制約がある。
/// </para>
/// </remarks>
public sealed class WorkSession : Entity
{
    public const int MaxAbandonNoteLength = 1000;

    /// <summary>EF Core 用。</summary>
    private WorkSession()
    {
        PreWorkState = null!;
        WorkContext = null!;
    }

    private WorkSession(
        long taskItemId,
        long workTypeId,
        long? plannedWorkId,
        PreWorkStateInput preWorkState,
        WorkContextInput workContext,
        DateTimeOffset now)
    {
        TaskItemId = taskItemId;
        WorkTypeId = workTypeId;
        PlannedWorkId = plannedWorkId;
        StartedAt = now;
        FinishedAt = null;
        Status = SessionStatus.InProgress;
        InterruptionCount = 0;

        // WS-1: 子は集約ルートが同一時刻で生成する。これにより PS-3 が保証される。
        PreWorkState = new PreWorkState(preWorkState, now);
        WorkContext = new WorkContext(workContext, now);

        CreatedAt = now;
        UpdatedAt = now;
    }

    public long TaskItemId { get; private set; }

    /// <summary>
    /// 作業タイプの<b>実績値</b>。分析 A-01 / A-02 はこの値を使う。
    /// TaskItem の既定値ではない。同じタスクでも回によって作業の性質が変わるため
    /// （docs/02-glossary.md §1）。
    /// </summary>
    public long WorkTypeId { get; private set; }

    /// <summary>
    /// 予定から開始した場合の PlannedWork。予定外の作業では null。
    /// null のセッションは実行率 A-06 の分母に含まれない。
    /// </summary>
    public long? PlannedWorkId { get; private set; }

    /// <summary>開始時刻（UTC）。外部から設定できない（WS-8）。</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>終了時刻（UTC）。進行中は null。</summary>
    public DateTimeOffset? FinishedAt { get; private set; }

    public SessionStatus Status { get; private set; }

    public int InterruptionCount { get; private set; }

    /// <summary>中断終了の理由メモ。<see cref="SessionStatus.Abandoned"/> のときのみ。</summary>
    public string? AbandonNote { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>作業前状態。必ず存在する（WS-1）。生成後は変更されない（PS-2）。</summary>
    public PreWorkState PreWorkState { get; private set; }

    /// <summary>作業環境。必ず存在する（WS-1）。生成後は変更されない（WC-3）。</summary>
    public WorkContext WorkContext { get; private set; }

    /// <summary>
    /// 成果評価。<see cref="SessionStatus.Completed"/> のとき必ず存在し（WS-3）、
    /// それ以外では null（WS-2 / WS-4）。
    /// </summary>
    public PerformanceResult? Result { get; private set; }

    public SessionPeriod Period => new(StartedAt, FinishedAt);

    /// <summary>実作業時間。未終了なら null。</summary>
    public int? DurationMinutes => Period.DurationMinutes;

    /// <summary>このセッションが集計される JST 基準の日付。開始時刻から決まる。</summary>
    public DateOnly BelongingDate => Period.BelongingDate;

    /// <summary>分析 A-03 で使う時間帯区分。</summary>
    public TimeBand TimeBand => Period.TimeBand;

    /// <summary>
    /// 疲労増加量（終了時疲労度 − 作業前疲労度）。−4〜+4。未評価なら null。
    /// </summary>
    /// <remarks>
    /// 導出可能な値のため保存しない。保存すると元データとの不整合が生じうる
    /// （docs/04-analytics-spec.md §5）。
    /// </remarks>
    public int? FatigueDelta => Result is null
        ? null
        : Result.FatigueAfter.Value - PreWorkState.FatigueLevel.Value;

    /// <summary>
    /// 見込みとの差（実際の集中度 − 見込み集中度）。−4〜+4。未評価なら null。
    /// 常に負なら、自分の状態認識が楽観的すぎることを意味する。
    /// </summary>
    public int? FocusGap => Result is null
        ? null
        : Result.FocusLevel.Value - PreWorkState.ExpectedFocusLevel.Value;

    /// <summary>
    /// 作業を開始する。WorkSession / PreWorkState / WorkContext が同時に生成される（WS-1）。
    /// </summary>
    /// <param name="now">
    /// 開始時刻。<see cref="IClock"/> から渡す。クライアントから受け取った値を渡さないこと（WS-8）。
    /// 記録し忘れたセッションを後から作れないのは意図的である。
    /// </param>
    public static WorkSession Start(
        long taskItemId,
        long workTypeId,
        long? plannedWorkId,
        PreWorkStateInput preWorkState,
        WorkContextInput workContext,
        DateTimeOffset now)
        => new(taskItemId, workTypeId, plannedWorkId, preWorkState, workContext, now);

    /// <summary>
    /// 作業を終了し、成果を記録する。終了と評価は分離できない（WS-3）。
    /// </summary>
    public void Finish(int interruptionCount, PerformanceResultInput result, DateTimeOffset now)
    {
        EnsureInProgress();
        EnsureValidInterruptionCount(interruptionCount);

        // WS-5。SessionPeriod の生成で検査する。
        _ = new SessionPeriod(StartedAt, now);

        FinishedAt = now;
        Status = SessionStatus.Completed;
        InterruptionCount = interruptionCount;
        Result = new PerformanceResult(result, now);
        UpdatedAt = now;
    }

    /// <summary>
    /// 作業として成立しなかったセッションを終了する。成果は記録されない（WS-4）。
    /// </summary>
    /// <remarks>
    /// 終端状態を与えることで、記録として保持しつつ分析から除外できる。
    /// 削除ではなく状態で表現する（docs/01-product-requirements.md §8 原則 2）。
    /// </remarks>
    public void Abandon(string? note, DateTimeOffset now)
    {
        EnsureInProgress();

        _ = new SessionPeriod(StartedAt, now);

        FinishedAt = now;
        Status = SessionStatus.Abandoned;
        AbandonNote = NormalizeAbandonNote(note);
        UpdatedAt = now;
    }

    /// <summary>
    /// 成果評価を訂正する。<see cref="RecordedAt"/> は変更されない。
    /// </summary>
    public void UpdateResult(PerformanceResultInput result, DateTimeOffset now)
    {
        EnsureCompleted();

        Result!.Update(result, now);
        UpdatedAt = now;
    }

    /// <summary>中断回数を訂正する。</summary>
    public void UpdateInterruptionCount(int interruptionCount, DateTimeOffset now)
    {
        EnsureCompleted();
        EnsureValidInterruptionCount(interruptionCount);

        if (interruptionCount == InterruptionCount)
        {
            return;
        }

        InterruptionCount = interruptionCount;
        UpdatedAt = now;
    }

    private void EnsureInProgress()
    {
        // WS-7: 終端状態から他の状態へ遷移しない。
        if (Status != SessionStatus.InProgress)
        {
            throw new DomainException(
                $"進行中の作業セッションではないため、この操作はできません: {Status}");
        }
    }

    private void EnsureCompleted()
    {
        if (Status != SessionStatus.Completed)
        {
            throw new DomainException(
                $"完了した作業セッションではないため、この操作はできません: {Status}");
        }
    }

    private static void EnsureValidInterruptionCount(int interruptionCount)
    {
        // WS-6
        if (interruptionCount < 0)
        {
            throw new DomainException($"中断回数は 0 以上である必要があります: {interruptionCount}");
        }
    }

    private static string? NormalizeAbandonNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var trimmed = note.Trim();

        if (trimmed.Length > MaxAbandonNoteLength)
        {
            throw new DomainException(
                $"中断終了のメモは {MaxAbandonNoteLength} 文字以内である必要があります: {trimmed.Length} 文字");
        }

        return trimmed;
    }
}
