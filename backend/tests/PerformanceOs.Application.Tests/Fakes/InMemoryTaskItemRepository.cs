using PerformanceOs.Domain.Repositories;
using PerformanceOs.Domain.TaskItems;

namespace PerformanceOs.Application.Tests.Fakes;

internal sealed class InMemoryTaskItemRepository : ITaskItemRepository
{
    private readonly List<TaskItem> _items = [];
    private long _nextId = 1;

    public IReadOnlyList<TaskItem> Items => _items;

    public TaskItem Seed(string title, long defaultWorkTypeId, bool isArchived = false)
    {
        var taskItem = TaskItem.Create(title, defaultWorkTypeId, null, DateTimeOffset.UnixEpoch);
        AssignId(taskItem, _nextId++);

        if (isArchived)
        {
            taskItem.Archive(DateTimeOffset.UnixEpoch);
        }

        _items.Add(taskItem);
        return taskItem;
    }

    public Task<IReadOnlyList<TaskItem>> GetAsync(
        bool includeArchived, string? keyword, CancellationToken cancellationToken)
    {
        IReadOnlyList<TaskItem> result = _items
            .Where(x => includeArchived || !x.IsArchived)
            .Where(x => string.IsNullOrWhiteSpace(keyword)
                        || x.Title.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<TaskItem?> GetByIdAsync(long id, CancellationToken cancellationToken)
        => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<TaskItem>> GetRecentlyUsedAsync(
        int limit, CancellationToken cancellationToken)
    {
        IReadOnlyList<TaskItem> result = _items
            .Where(x => !x.IsArchived)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult(result);
    }

    public Task AddAsync(TaskItem taskItem, CancellationToken cancellationToken)
    {
        AssignId(taskItem, _nextId++);
        _items.Add(taskItem);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TaskItem taskItem, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private static void AssignId(TaskItem taskItem, long id)
        => typeof(Domain.Common.Entity)
            .GetProperty(nameof(Domain.Common.Entity.Id))!
            .SetValue(taskItem, id);
}
