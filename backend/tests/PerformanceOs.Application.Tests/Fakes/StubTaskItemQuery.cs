using PerformanceOs.Application.TaskItems;

namespace PerformanceOs.Application.Tests.Fakes;

/// <summary>
/// 読み取りモデルのスタブ。射影の中身は SQL 側の責務であり、
/// 統合テスト（T-11）で確認する。ここではサービスが委譲することだけを見る。
/// </summary>
internal sealed class StubTaskItemQuery : ITaskItemQuery
{
    public bool? LastIncludeArchived { get; private set; }

    public string? LastKeyword { get; private set; }

    public TaskItemSort? LastSort { get; private set; }

    public List<TaskItemSummary> Summaries { get; } = [];

    public Task<IReadOnlyList<TaskItemSummary>> GetSummariesAsync(
        bool includeArchived, string? keyword, TaskItemSort sort, CancellationToken cancellationToken)
    {
        LastIncludeArchived = includeArchived;
        LastKeyword = keyword;
        LastSort = sort;

        return Task.FromResult<IReadOnlyList<TaskItemSummary>>(Summaries);
    }
}
