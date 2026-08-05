using PerformanceOs.Domain.DailyConditions;

namespace PerformanceOs.Domain.Repositories;

/// <summary>
/// 日次コンディションの永続化（docs/05-domain-design.md §7）。
/// </summary>
/// <remarks>
/// 日付はすべて JST 基準の論理日である（docs/02-glossary.md §4）。
/// </remarks>
public interface IDailyConditionRepository
{
    Task<DailyCondition?> GetByDateAsync(DateOnly jstDate, CancellationToken cancellationToken);

    /// <summary>
    /// 期間内のコンディションを対象日の昇順で返す（両端を含む）。
    /// </summary>
    /// <remarks>
    /// <b>未記録の日は返らない。</b>欠損を埋めないこと。分析側で除外し、
    /// 除外件数を示す（docs/04-analytics-spec.md A-05）。
    /// </remarks>
    Task<IReadOnlyList<DailyCondition>> GetByDateRangeAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task AddAsync(DailyCondition dailyCondition, CancellationToken cancellationToken);

    Task UpdateAsync(DailyCondition dailyCondition, CancellationToken cancellationToken);
}
