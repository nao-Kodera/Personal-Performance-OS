namespace PerformanceOs.Application.TaskItems;

/// <summary>タスク一覧の並び順（docs/07-api-design.md §2.4）。</summary>
public enum TaskItemSort
{
    /// <summary>直近に作業した順。作業開始画面の選択を速くするため。</summary>
    Recent,

    /// <summary>更新日時の降順。</summary>
    Updated,
}

/// <summary>
/// タスク一覧の 1 行（docs/07-api-design.md §2.4）。
/// </summary>
/// <remarks>
/// 集約ではなく射影である。読み取り専用の一覧は集約を経由しない
/// （docs/08-technical-design.md §3.7）。
/// </remarks>
/// <param name="LastUsedAt">
/// このタスクの最新セッションの開始時刻。一度も使われていなければ null。
/// </param>
public sealed record TaskItemSummary(
    long Id,
    string Title,
    long DefaultWorkTypeId,
    string DefaultWorkTypeName,
    string? Note,
    bool IsArchived,
    DateTimeOffset? LastUsedAt,
    int SessionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
