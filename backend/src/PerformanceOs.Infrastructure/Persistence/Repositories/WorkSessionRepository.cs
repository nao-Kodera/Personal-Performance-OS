using Microsoft.EntityFrameworkCore;
using PerformanceOs.Domain.Repositories;
using PerformanceOs.Domain.Time;
using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Infrastructure.Persistence.Repositories;

public sealed class WorkSessionRepository : IWorkSessionRepository
{
    private readonly PerformanceOsDbContext _db;

    public WorkSessionRepository(PerformanceOsDbContext db)
    {
        _db = db;
    }

    public Task<WorkSession?> GetActiveAsync(CancellationToken cancellationToken)
        => WithAggregate()
            .FirstOrDefaultAsync(x => x.Status == SessionStatus.InProgress, cancellationToken);

    public Task<WorkSession?> GetByIdAsync(long id, CancellationToken cancellationToken)
        => WithAggregate().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WorkSession>> GetByDateRangeAsync(
        DateOnly fromJst, DateOnly toJst, SessionStatus? status, CancellationToken cancellationToken)
    {
        // JST の日付範囲を UTC の瞬間に変換してから比較する。
        // AT TIME ZONE を含む式で絞り込むと ix_work_sessions_started_at が使えない。
        var fromUtc = JstCalendar.StartOfDayUtc(fromJst);
        var toExclusiveUtc = JstCalendar.StartOfDayUtc(toJst.AddDays(1));

        var query = WithAggregate()
            .Where(x => x.StartedAt >= fromUtc && x.StartedAt < toExclusiveUtc);

        // nullable のまま比較すると、値変換された列との突き合わせで
        // 翻訳に失敗しうる。非 nullable の局所変数にしてから比較する。
        if (status is { } sessionStatus)
        {
            query = query.Where(x => x.Status == sessionStatus);
        }

        return await query
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsByPlannedWorkIdAsync(long plannedWorkId, CancellationToken cancellationToken)
        => _db.WorkSessions.AnyAsync(x => x.PlannedWorkId == plannedWorkId, cancellationToken);

    public async Task AddAsync(WorkSession workSession, CancellationToken cancellationToken)
    {
        // WorkSession / PreWorkState / WorkContext は 1 回の SaveChanges で
        // 保存される。EF Core は同一 SaveChanges 内の変更を単一トランザクションに
        // まとめるため、WS-1 が破れることはない（docs/07-api-design.md §4）。
        await _db.WorkSessions.AddAsync(workSession, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WorkSession workSession, CancellationToken cancellationToken)
    {
        // 終了時は WorkSession の更新と PerformanceResult の挿入が同時に起きる。
        // これも 1 回の SaveChanges で完結する（WS-3）。
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 集約全体を読み込む。部分的に読み込まない。状態遷移の検証（WS-2〜4）に
    /// 子の有無が必要なため（docs/08-technical-design.md §3.5）。
    /// </summary>
    private IQueryable<WorkSession> WithAggregate()
        => _db.WorkSessions
            .Include(x => x.PreWorkState)
            .Include(x => x.WorkContext)
            .Include(x => x.Result);
}
