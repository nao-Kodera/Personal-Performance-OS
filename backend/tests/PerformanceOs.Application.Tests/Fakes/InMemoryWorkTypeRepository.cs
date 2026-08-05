using PerformanceOs.Domain.Repositories;
using PerformanceOs.Domain.WorkTypes;

namespace PerformanceOs.Application.Tests.Fakes;

/// <summary>
/// モックライブラリを使わない方針によるインメモリ実装
/// （docs/08-technical-design.md §6.1）。
/// </summary>
internal sealed class InMemoryWorkTypeRepository : IWorkTypeRepository
{
    private readonly List<WorkType> _items = [];
    private long _nextId = 1;

    /// <summary>テスト準備用。ID を採番して直接投入する。</summary>
    public WorkType Seed(string name, int displayOrder, bool isActive = true)
    {
        var workType = WorkType.Create(name, displayOrder, DateTimeOffset.UnixEpoch);
        AssignId(workType, _nextId++);

        if (!isActive)
        {
            workType.Deactivate(DateTimeOffset.UnixEpoch);
        }

        _items.Add(workType);
        return workType;
    }

    public Task<IReadOnlyList<WorkType>> GetAllAsync(
        bool includeInactive, CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkType> result = _items
            .Where(x => includeInactive || x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<WorkType?> GetByIdAsync(long id, CancellationToken cancellationToken)
        => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

    public Task<bool> ExistsByNameAsync(string name, long? excludeId, CancellationToken cancellationToken)
    {
        var normalized = name.Trim();

        return Task.FromResult(_items.Any(
            x => string.Equals(x.Name, normalized, StringComparison.OrdinalIgnoreCase)
                 && (excludeId is null || x.Id != excludeId)));
    }

    public Task AddAsync(WorkType workType, CancellationToken cancellationToken)
    {
        AssignId(workType, _nextId++);
        _items.Add(workType);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(WorkType workType, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Id は DB が採番するため setter が無い。テストでは実 DB の挙動を
    /// 再現するためリフレクションで設定する。
    /// </summary>
    private static void AssignId(WorkType workType, long id)
        => typeof(Domain.Common.Entity)
            .GetProperty(nameof(Domain.Common.Entity.Id))!
            .SetValue(workType, id);
}
