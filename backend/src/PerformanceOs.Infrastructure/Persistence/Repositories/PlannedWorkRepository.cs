using Microsoft.EntityFrameworkCore;
using PerformanceOs.Domain.PlannedWorks;
using PerformanceOs.Domain.Repositories;

namespace PerformanceOs.Infrastructure.Persistence.Repositories;

/// <summary>
/// 予定の永続化。
/// </summary>
/// <remarks>
/// 集約内の <see cref="NonExecutionRecord"/> は
/// <c>Navigation(...).AutoInclude()</c> により常に読み込まれる。Include 漏れで
/// 「未実行記録が無い」と誤判定すると、PW-4 / PW-5 の検査がすり抜ける。
/// </remarks>
public sealed class PlannedWorkRepository : IPlannedWorkRepository
{
    private readonly PerformanceOsDbContext _db;

    public PlannedWorkRepository(PerformanceOsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PlannedWork>> GetByDateAsync(
        DateOnly jstDate, CancellationToken cancellationToken)
        => await _db.PlannedWorks
            .Where(x => x.TargetDate == jstDate)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<PlannedWork?> GetByIdAsync(long id, CancellationToken cancellationToken)
        => _db.PlannedWorks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PlannedWork>> GetByDateRangeAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => await _db.PlannedWorks
            .Where(x => x.TargetDate >= from && x.TargetDate <= to)
            .OrderBy(x => x.TargetDate)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(PlannedWork plannedWork, CancellationToken cancellationToken)
    {
        await _db.PlannedWorks.AddAsync(plannedWork, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PlannedWork plannedWork, CancellationToken cancellationToken)
    {
        _db.PlannedWorks.Update(plannedWork);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
