using PerformanceOs.Domain.PlannedWorks;

namespace PerformanceOs.Domain.Repositories;

/// <summary>
/// 作業予定の永続化（docs/05-domain-design.md §7）。
/// </summary>
/// <remarks>
/// 日付はすべて JST 基準の論理日である。取得時は集約全体（NonExecution を含む）
/// を読む（docs/08-technical-design.md §3.5）。
/// </remarks>
public interface IPlannedWorkRepository
{
    Task<IReadOnlyList<PlannedWork>> GetByDateAsync(
        DateOnly jstDate, CancellationToken cancellationToken);

    Task<PlannedWork?> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlannedWork>> GetByDateRangeAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task AddAsync(PlannedWork plannedWork, CancellationToken cancellationToken);

    Task UpdateAsync(PlannedWork plannedWork, CancellationToken cancellationToken);
}
