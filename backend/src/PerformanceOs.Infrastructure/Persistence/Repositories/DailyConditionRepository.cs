using Microsoft.EntityFrameworkCore;
using PerformanceOs.Domain.DailyConditions;
using PerformanceOs.Domain.Repositories;

namespace PerformanceOs.Infrastructure.Persistence.Repositories;

public sealed class DailyConditionRepository : IDailyConditionRepository
{
    private readonly PerformanceOsDbContext _db;

    public DailyConditionRepository(PerformanceOsDbContext db)
    {
        _db = db;
    }

    public Task<DailyCondition?> GetByDateAsync(
        DateOnly jstDate, CancellationToken cancellationToken)
        => _db.DailyConditions
            .FirstOrDefaultAsync(x => x.TargetDate == jstDate, cancellationToken);

    public async Task<IReadOnlyList<DailyCondition>> GetByDateRangeAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => await _db.DailyConditions
            .Where(x => x.TargetDate >= from && x.TargetDate <= to)
            .OrderBy(x => x.TargetDate)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(DailyCondition dailyCondition, CancellationToken cancellationToken)
    {
        await _db.DailyConditions.AddAsync(dailyCondition, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(DailyCondition dailyCondition, CancellationToken cancellationToken)
    {
        _db.DailyConditions.Update(dailyCondition);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
