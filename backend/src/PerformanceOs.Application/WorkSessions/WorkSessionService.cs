using PerformanceOs.Application.Common;
using PerformanceOs.Domain.Repositories;
using PerformanceOs.Domain.Time;
using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Application.WorkSessions;

/// <summary>
/// 作業セッションのユースケース。本プロダクトの中核。
/// </summary>
/// <remarks>
/// <para>
/// ドメイン設計 §6.1 の WorkSessionStarter に相当する横断検証を
/// <see cref="StartAsync"/> に統合している。技術設計 §2.2 は
/// WorkSessionStarter を Domain に置くが、その責務は 409 / 422 に対応する
/// 例外を投げることであり、それらは Application.Common にある。
/// Domain は Application に依存できないため（§2.1）、ここに置く。
/// 別クラスに切り出すと §2.3 の「ユースケースごとにクラスを作らない」に反する。
/// </para>
/// </remarks>
public sealed class WorkSessionService
{
    private const int LongSessionMinutes = 8 * 60;

    private readonly IWorkSessionRepository _workSessions;
    private readonly ITaskItemRepository _taskItems;
    private readonly IWorkTypeRepository _workTypes;
    private readonly IClock _clock;

    public WorkSessionService(
        IWorkSessionRepository workSessions,
        ITaskItemRepository taskItems,
        IWorkTypeRepository workTypes,
        IClock clock)
    {
        _workSessions = workSessions;
        _taskItems = taskItems;
        _workTypes = workTypes;
        _clock = clock;
    }

    /// <summary>進行中のセッション。存在しなければ null。</summary>
    public Task<WorkSession?> GetActiveAsync(CancellationToken cancellationToken)
        => _workSessions.GetActiveAsync(cancellationToken);

    public async Task<WorkSession> GetByIdAsync(long id, CancellationToken cancellationToken)
        => await _workSessions.GetByIdAsync(id, cancellationToken)
           ?? throw NotFoundException.For("作業セッション", id);

    /// <summary>
    /// 作業を開始する。WorkSession / PreWorkState / WorkContext が
    /// 同一トランザクションで保存される（WS-1）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 開始時刻はサーバー時刻を使う。リクエストから受け取らない（WS-8）。
    /// </para>
    /// <para>
    /// 予定からの開始（plannedWorkId）は T-21 で追加する。検証も外部キー制約も
    /// PlannedWork の実装が前提になるため、引数として先に用意しない。
    /// </para>
    /// </remarks>
    public async Task<WorkSession> StartAsync(
        long taskItemId,
        long workTypeId,
        PreWorkStateInput preWorkState,
        WorkContextInput workContext,
        CancellationToken cancellationToken)
    {
        await EnsureNoActiveSessionAsync(cancellationToken);
        await EnsureTaskItemIsUsableAsync(taskItemId, cancellationToken);
        await EnsureWorkTypeIsUsableAsync(workTypeId, cancellationToken);

        var session = WorkSession.Start(
            taskItemId,
            workTypeId,
            plannedWorkId: null,
            preWorkState,
            workContext,
            _clock.UtcNow);

        await _workSessions.AddAsync(session, cancellationToken);

        return session;
    }

    /// <summary>
    /// 作業を終了し、成果を記録する。
    /// </summary>
    /// <remarks>
    /// 終了と評価は分離できない。分離すると「終了したが評価していない」状態が
    /// 生じ、WS-3 が破れる。成果評価のスキップ導線を作らないこと。
    /// </remarks>
    public async Task<FinishedWorkSession> FinishAsync(
        long id,
        int interruptionCount,
        PerformanceResultInput result,
        CancellationToken cancellationToken)
    {
        var session = await GetByIdAsync(id, cancellationToken);

        EnsureStatusIs(session, SessionStatus.InProgress);

        var now = _clock.UtcNow;
        EnsureTimeHasAdvanced(session, now);

        session.Finish(interruptionCount, result, now);

        await _workSessions.UpdateAsync(session, cancellationToken);

        return new FinishedWorkSession(session, DetectWarnings(session));
    }

    /// <summary>
    /// 作業として成立しなかったセッションを終了する。成果は記録されない。
    /// </summary>
    public async Task<WorkSession> AbandonAsync(
        long id, string? note, CancellationToken cancellationToken)
    {
        var session = await GetByIdAsync(id, cancellationToken);

        EnsureStatusIs(session, SessionStatus.InProgress);

        var now = _clock.UtcNow;
        EnsureTimeHasAdvanced(session, now);

        session.Abandon(note, now);

        await _workSessions.UpdateAsync(session, cancellationToken);

        return session;
    }

    /// <summary>
    /// 成果評価と中断回数を訂正する。初回記録時刻は変更されない（PR-2）。
    /// </summary>
    public async Task<WorkSession> UpdateResultAsync(
        long id,
        int interruptionCount,
        PerformanceResultInput result,
        CancellationToken cancellationToken)
    {
        var session = await GetByIdAsync(id, cancellationToken);

        EnsureStatusIs(session, SessionStatus.Completed);

        var now = _clock.UtcNow;

        session.UpdateResult(result, now);
        session.UpdateInterruptionCount(interruptionCount, now);

        await _workSessions.UpdateAsync(session, cancellationToken);

        return session;
    }

    /// <summary>
    /// 履歴を JST の日付単位でまとめて返す。日付・セッションとも新しい順。
    /// </summary>
    /// <param name="fromJst">開始日（この日を含む・JST 基準）。</param>
    /// <param name="toJst">終了日（この日を含む・JST 基準）。</param>
    public async Task<IReadOnlyList<WorkSessionDay>> GetHistoryAsync(
        DateOnly fromJst,
        DateOnly toJst,
        SessionStatus? status,
        long? taskItemId,
        CancellationToken cancellationToken)
    {
        if (fromJst > toJst)
        {
            throw new DomainRuleException(
                $"開始日は終了日以前である必要があります: {fromJst:yyyy-MM-dd} 〜 {toJst:yyyy-MM-dd}");
        }

        var sessions = await _workSessions.GetByDateRangeAsync(
            fromJst, toJst, status, cancellationToken);

        // タスクでの絞り込みはメモリ上で行う。期間で既に絞られており、
        // 想定規模（3 年で約 2,000 セッション）では実害がない
        // （docs/04-analytics-spec.md §7）。
        if (taskItemId is not null)
        {
            sessions = sessions.Where(x => x.TaskItemId == taskItemId).ToList();
        }

        return sessions
            .GroupBy(x => x.BelongingDate)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var ordered = g.OrderByDescending(x => x.StartedAt).ToList();

                return new WorkSessionDay(
                    g.Key,
                    ordered,
                    ordered.Count(x => x.Status == SessionStatus.Completed),
                    ordered.Count(x => x.Status == SessionStatus.Abandoned),
                    ordered.Sum(x => x.DurationMinutes ?? 0));
            })
            .ToList();
    }

    /// <summary>
    /// 進行中のセッションが存在しないことを確認する（WS-9）。
    /// </summary>
    /// <remarks>
    /// このチェックだけでは同時実行を防げない。複数タブからの操作や
    /// リトライで並行リクエストが起きる。DB の部分一意インデックス
    /// <c>uq_work_sessions_single_active</c> が最終的な担保であり、
    /// ここでの検査は分かりやすいエラーを返すためのものである
    /// （docs/08-technical-design.md §3.6）。
    /// </remarks>
    private async Task EnsureNoActiveSessionAsync(CancellationToken cancellationToken)
    {
        if (await _workSessions.GetActiveAsync(cancellationToken) is not null)
        {
            throw new ConflictException("進行中の作業セッションが既に存在します。");
        }
    }

    private async Task EnsureTaskItemIsUsableAsync(long taskItemId, CancellationToken cancellationToken)
    {
        var taskItem = await _taskItems.GetByIdAsync(taskItemId, cancellationToken);

        if (taskItem is null)
        {
            throw new DomainRuleException($"タスクが存在しません: id={taskItemId}");
        }

        if (taskItem.IsArchived)
        {
            throw new DomainRuleException($"アーカイブ済みのタスクでは開始できません: {taskItem.Title}");
        }
    }

    private async Task EnsureWorkTypeIsUsableAsync(long workTypeId, CancellationToken cancellationToken)
    {
        var workType = await _workTypes.GetByIdAsync(workTypeId, cancellationToken);

        if (workType is null)
        {
            throw new DomainRuleException($"作業タイプが存在しません: id={workTypeId}");
        }

        if (!workType.IsActive)
        {
            throw new DomainRuleException($"無効な作業タイプでは開始できません: {workType.Name}");
        }
    }

    private static void EnsureStatusIs(WorkSession session, SessionStatus expected)
    {
        if (session.Status != expected)
        {
            throw new ConflictException(
                $"作業セッションの状態が {expected} ではないため、この操作はできません: {session.Status}");
        }
    }

    /// <summary>
    /// 終了時刻が開始時刻より後であることを確認する（WS-5）。
    /// </summary>
    /// <remarks>
    /// サーバー時刻が巻き戻った場合にのみ発生する。ドメイン層でも検査されるが、
    /// そちらは 422 に対応する例外になる。この状況は値の誤りではなく状態の
    /// 矛盾のため、409 として扱う（docs/07-api-design.md §2.15）。
    /// </remarks>
    private static void EnsureTimeHasAdvanced(WorkSession session, DateTimeOffset now)
    {
        if (now <= session.StartedAt)
        {
            throw new ConflictException(
                $"現在時刻が開始時刻以前です: 開始 {session.StartedAt:O} / 現在 {now:O}");
        }
    }

    private static IReadOnlyList<SessionWarning> DetectWarnings(WorkSession session)
    {
        var warnings = new List<SessionWarning>();

        var minutes = session.DurationMinutes ?? 0;

        if (minutes > LongSessionMinutes)
        {
            warnings.Add(SessionWarning.LongSession);
        }

        if (minutes < 1)
        {
            warnings.Add(SessionWarning.VeryShortSession);
        }

        // MissingDailyCondition は DailyCondition（T-20）の実装後に判定する。

        return warnings;
    }
}
