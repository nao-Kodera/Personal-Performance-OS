using Microsoft.EntityFrameworkCore;
using PerformanceOs.Domain.Repositories;
using PerformanceOs.Domain.TaskItems;

namespace PerformanceOs.Infrastructure.Persistence.Repositories;

public sealed class TaskItemRepository : ITaskItemRepository
{
    private readonly PerformanceOsDbContext _db;

    public TaskItemRepository(PerformanceOsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TaskItem>> GetAsync(
        bool includeArchived, string? keyword, CancellationToken cancellationToken)
    {
        var query = _db.TaskItems.AsQueryable();

        if (!includeArchived)
        {
            query = query.Where(x => !x.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Title, pattern));
        }

        return await query
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<TaskItem?> GetByIdAsync(long id, CancellationToken cancellationToken)
        => _db.TaskItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TaskItem>> GetRecentlyUsedAsync(
        int limit, CancellationToken cancellationToken)
    {
        // 各タスクの最終利用時刻（そのタスクの最新セッションの開始時刻）で並べる。
        // ループ内でクエリを発行せず、1 本のクエリにまとめる。
        var lastUsed = _db.WorkSessions
            .GroupBy(s => s.TaskItemId)
            .Select(g => new { TaskItemId = g.Key, LastUsedAt = g.Max(s => s.StartedAt) });

        return await _db.TaskItems
            .Where(t => !t.IsArchived)
            .GroupJoin(
                lastUsed,
                t => t.Id,
                u => u.TaskItemId,
                (t, u) => new { Task = t, Used = u })
            .SelectMany(
                x => x.Used.DefaultIfEmpty(),
                (x, u) => new { x.Task, u!.LastUsedAt })
            .OrderByDescending(x => x.LastUsedAt)
            .ThenByDescending(x => x.Task.UpdatedAt)
            .Take(limit)
            .Select(x => x.Task)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TaskItem taskItem, CancellationToken cancellationToken)
    {
        await _db.TaskItems.AddAsync(taskItem, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TaskItem taskItem, CancellationToken cancellationToken)
    {
        _db.TaskItems.Update(taskItem);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
