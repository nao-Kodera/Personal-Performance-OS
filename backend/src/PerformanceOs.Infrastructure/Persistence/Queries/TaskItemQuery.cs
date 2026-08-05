using Microsoft.EntityFrameworkCore;
using PerformanceOs.Application.TaskItems;

namespace PerformanceOs.Infrastructure.Persistence.Queries;

/// <summary>
/// タスク一覧の射影。集約を経由しない（docs/08-technical-design.md §3.7）。
/// </summary>
public sealed class TaskItemQuery : ITaskItemQuery
{
    private readonly PerformanceOsDbContext _db;

    public TaskItemQuery(PerformanceOsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TaskItemSummary>> GetSummariesAsync(
        bool includeArchived,
        string? keyword,
        TaskItemSort sort,
        CancellationToken cancellationToken)
    {
        var tasks = _db.TaskItems.AsNoTracking();

        if (!includeArchived)
        {
            tasks = tasks.Where(t => !t.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";
            tasks = tasks.Where(t => EF.Functions.ILike(t.Title, pattern));
        }

        // 相関副問い合わせで 1 本の SQL にまとめる。タスクごとにクエリを
        // 発行しない。
        //
        // 並べ替えは匿名型に対して行い、TaskItemSummary への射影は最後にする。
        // TaskItemSummary はコンストラクタを持つレコードであり、EF Core は
        // その中身を透過的に扱えないため、射影後のプロパティで OrderBy すると
        // 翻訳に失敗する。
        var rows = from t in tasks
                   join wt in _db.WorkTypes.AsNoTracking()
                       on t.DefaultWorkTypeId equals wt.Id
                   select new
                   {
                       Task = t,
                       WorkTypeName = wt.Name,
                       LastUsedAt = _db.WorkSessions
                           .Where(s => s.TaskItemId == t.Id)
                           .Max(s => (DateTimeOffset?)s.StartedAt),
                       SessionCount = _db.WorkSessions.Count(s => s.TaskItemId == t.Id),
                   };

        // Recent では未使用のタスクを末尾に置く。PostgreSQL の DESC は
        // 既定で NULLS FIRST のため、有無を先に並べる。
        var ordered = sort switch
        {
            TaskItemSort.Recent => rows
                .OrderByDescending(x => x.LastUsedAt != null)
                .ThenByDescending(x => x.LastUsedAt)
                .ThenByDescending(x => x.Task.UpdatedAt),
            _ => rows
                .OrderByDescending(x => x.Task.UpdatedAt)
                .ThenByDescending(x => x.Task.Id),
        };

        return await ordered
            .Select(x => new TaskItemSummary(
                x.Task.Id,
                x.Task.Title,
                x.Task.DefaultWorkTypeId,
                x.WorkTypeName,
                x.Task.Note,
                x.Task.IsArchived,
                x.LastUsedAt,
                x.SessionCount,
                x.Task.CreatedAt,
                x.Task.UpdatedAt))
            .ToListAsync(cancellationToken);
    }
}
