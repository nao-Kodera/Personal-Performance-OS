using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Domain.Repositories;

/// <summary>
/// 作業セッションの永続化（docs/05-domain-design.md §7）。
/// </summary>
/// <remarks>
/// 読み取りは常に集約全体（PreWorkState / WorkContext / Result）を読み込む。
/// 状態遷移の検証に子の有無が必要なためである（docs/08-technical-design.md §3.5）。
/// 分析用の集計は集約を経由せず、別途リードモデルで行う。
/// </remarks>
public interface IWorkSessionRepository
{
    /// <summary>
    /// 進行中のセッションを返す。存在しなければ null。
    /// </summary>
    /// <remarks>
    /// 進行中は全体で最大 1 件のため単一を返す（WS-9）。
    /// ただしこの検査だけでは同時実行を防げない。並行リクエストでは
    /// DB の部分一意インデックス <c>uq_work_sessions_single_active</c> が
    /// 最終的な担保になる（docs/08-technical-design.md §3.6）。
    /// </remarks>
    Task<WorkSession?> GetActiveAsync(CancellationToken cancellationToken);

    Task<WorkSession?> GetByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// 指定した JST 日付範囲に<b>所属する</b>セッションを、開始時刻の降順で返す。
    /// </summary>
    /// <remarks>
    /// 所属日は開始時刻の JST 日付で決まる。22:00 開始・翌 01:00 終了の
    /// セッションは開始日側に含まれる（docs/02-glossary.md §4）。
    /// </remarks>
    /// <param name="fromJst">開始日（この日を含む）。</param>
    /// <param name="toJst">終了日（この日を含む）。</param>
    Task<IReadOnlyList<WorkSession>> GetByDateRangeAsync(
        DateOnly fromJst, DateOnly toJst, SessionStatus? status, CancellationToken cancellationToken);

    /// <summary>指定の予定に紐づくセッションが存在するか（PW-4 の検査に使う）。</summary>
    Task<bool> ExistsByPlannedWorkIdAsync(long plannedWorkId, CancellationToken cancellationToken);

    Task AddAsync(WorkSession workSession, CancellationToken cancellationToken);

    Task UpdateAsync(WorkSession workSession, CancellationToken cancellationToken);
}
