using PerformanceOs.Domain.TaskItems;

namespace PerformanceOs.Domain.Repositories;

/// <summary>
/// タスクの永続化（docs/05-domain-design.md §7）。
/// </summary>
public interface ITaskItemRepository
{
    Task<IReadOnlyList<TaskItem>> GetAsync(
        bool includeArchived, string? keyword, CancellationToken cancellationToken);

    Task<TaskItem?> GetByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// 直近に作業したタスクを新しい順に返す。
    /// </summary>
    /// <remarks>
    /// 作業開始画面のタスク選択を速くするために使う。作業前の入力は
    /// 30 秒以内に収める必要がある（docs/01-product-requirements.md §8 原則 1）。
    /// アーカイブ済みは含めない。
    /// </remarks>
    Task<IReadOnlyList<TaskItem>> GetRecentlyUsedAsync(int limit, CancellationToken cancellationToken);

    Task AddAsync(TaskItem taskItem, CancellationToken cancellationToken);

    Task UpdateAsync(TaskItem taskItem, CancellationToken cancellationToken);
}
