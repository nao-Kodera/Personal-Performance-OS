namespace PerformanceOs.Application.TaskItems;

/// <summary>
/// タスク一覧の読み取りモデル。実装は Infrastructure に置く。
/// </summary>
/// <remarks>
/// リポジトリと分けているのは、返すものが集約ではなく射影だからである。
/// セッション数と最終利用時刻をサービス内でループして数えると N+1 になるため、
/// 1 本のクエリで取得する（docs/08-technical-design.md §3.7）。
/// </remarks>
public interface ITaskItemQuery
{
    Task<IReadOnlyList<TaskItemSummary>> GetSummariesAsync(
        bool includeArchived,
        string? keyword,
        TaskItemSort sort,
        CancellationToken cancellationToken);
}
