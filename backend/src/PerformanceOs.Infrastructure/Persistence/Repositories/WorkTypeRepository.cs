using Microsoft.EntityFrameworkCore;
using PerformanceOs.Domain.Repositories;
using PerformanceOs.Domain.WorkTypes;

namespace PerformanceOs.Infrastructure.Persistence.Repositories;

public sealed class WorkTypeRepository : IWorkTypeRepository
{
    private readonly PerformanceOsDbContext _db;

    public WorkTypeRepository(PerformanceOsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<WorkType>> GetAllAsync(
        bool includeInactive, CancellationToken cancellationToken)
    {
        var query = _db.WorkTypes.AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<WorkType?> GetByIdAsync(long id, CancellationToken cancellationToken)
        => _db.WorkTypes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> ExistsByNameAsync(string name, long? excludeId, CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToLowerInvariant();

        return _db.WorkTypes.AnyAsync(
            x => x.Name.ToLower() == normalized && (excludeId == null || x.Id != excludeId),
            cancellationToken);
    }

    public async Task AddAsync(WorkType workType, CancellationToken cancellationToken)
    {
        await _db.WorkTypes.AddAsync(workType, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WorkType workType, CancellationToken cancellationToken)
    {
        _db.WorkTypes.Update(workType);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
