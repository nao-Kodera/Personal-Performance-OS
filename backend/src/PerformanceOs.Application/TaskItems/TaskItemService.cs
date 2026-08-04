using PerformanceOs.Application.Common;
using PerformanceOs.Domain.Repositories;
using PerformanceOs.Domain.TaskItems;
using PerformanceOs.Domain.Time;

namespace PerformanceOs.Application.TaskItems;

/// <summary>
/// タスクのユースケース。
/// </summary>
/// <remarks>
/// <b>TaskItem に完了の概念はない。</b>使わなくなったものはアーカイブする。
/// 完了操作を追加しないこと（docs/08-technical-design.md §8 の禁止事項 2）。
/// </remarks>
public sealed class TaskItemService
{
    private readonly ITaskItemRepository _taskItems;
    private readonly ITaskItemQuery _taskItemQuery;
    private readonly IWorkTypeRepository _workTypes;
    private readonly IWorkSessionRepository _workSessions;
    private readonly IClock _clock;

    public TaskItemService(
        ITaskItemRepository taskItems,
        ITaskItemQuery taskItemQuery,
        IWorkTypeRepository workTypes,
        IWorkSessionRepository workSessions,
        IClock clock)
    {
        _taskItems = taskItems;
        _taskItemQuery = taskItemQuery;
        _workTypes = workTypes;
        _workSessions = workSessions;
        _clock = clock;
    }

    public Task<IReadOnlyList<TaskItemSummary>> GetAsync(
        bool includeArchived, string? keyword, TaskItemSort sort, CancellationToken cancellationToken)
        => _taskItemQuery.GetSummariesAsync(includeArchived, keyword, sort, cancellationToken);

    public async Task<TaskItem> GetByIdAsync(long id, CancellationToken cancellationToken)
        => await _taskItems.GetByIdAsync(id, cancellationToken)
           ?? throw NotFoundException.For("タスク", id);

    /// <remarks>
    /// 同名のタスクが既に存在してもエラーにしない。同じ名前の作業を別の機会に
    /// 行うのは正常である（TI-3）。
    /// </remarks>
    public async Task<TaskItem> CreateAsync(
        string title, long defaultWorkTypeId, string? note, CancellationToken cancellationToken)
    {
        await EnsureWorkTypeIsUsableAsync(defaultWorkTypeId, cancellationToken);

        var taskItem = TaskItem.Create(title, defaultWorkTypeId, note, _clock.UtcNow);

        await _taskItems.AddAsync(taskItem, cancellationToken);

        return taskItem;
    }

    public async Task<TaskItem> UpdateAsync(
        long id, string title, long defaultWorkTypeId, string? note, CancellationToken cancellationToken)
    {
        var taskItem = await GetByIdAsync(id, cancellationToken);

        await EnsureWorkTypeIsUsableAsync(defaultWorkTypeId, cancellationToken);

        taskItem.Update(title, defaultWorkTypeId, note, _clock.UtcNow);

        await _taskItems.UpdateAsync(taskItem, cancellationToken);

        return taskItem;
    }

    /// <summary>
    /// アーカイブする。<b>完了ではなく、選択肢からの除外である。</b>
    /// </summary>
    /// <remarks>
    /// 進行中のセッションを持つタスクはアーカイブできない（docs/07-api-design.md §2.7）。
    /// これは集約をまたぐ制約のため、ここで検査する。
    /// </remarks>
    public async Task<TaskItem> ArchiveAsync(long id, CancellationToken cancellationToken)
    {
        var taskItem = await GetByIdAsync(id, cancellationToken);

        var active = await _workSessions.GetActiveAsync(cancellationToken);
        if (active is not null && active.TaskItemId == id)
        {
            throw new ConflictException(
                "進行中の作業セッションがあるタスクはアーカイブできません。");
        }

        taskItem.Archive(_clock.UtcNow);

        await _taskItems.UpdateAsync(taskItem, cancellationToken);

        return taskItem;
    }

    public async Task<TaskItem> UnarchiveAsync(long id, CancellationToken cancellationToken)
    {
        var taskItem = await GetByIdAsync(id, cancellationToken);

        taskItem.Unarchive(_clock.UtcNow);

        await _taskItems.UpdateAsync(taskItem, cancellationToken);

        return taskItem;
    }

    /// <summary>
    /// 指定の作業タイプが存在し、有効であることを確認する（TI-2）。
    /// </summary>
    /// <remarks>
    /// 存在しない・無効はいずれも「何度試しても成功しない」ため 422 に対応する
    /// <see cref="DomainRuleException"/> を投げる（docs/07-api-design.md §0.2）。
    /// </remarks>
    private async Task EnsureWorkTypeIsUsableAsync(long workTypeId, CancellationToken cancellationToken)
    {
        var workType = await _workTypes.GetByIdAsync(workTypeId, cancellationToken);

        if (workType is null)
        {
            throw new DomainRuleException($"作業タイプが存在しません: id={workTypeId}");
        }

        if (!workType.IsActive)
        {
            throw new DomainRuleException($"無効な作業タイプは指定できません: {workType.Name}");
        }
    }
}
