using PerformanceOs.Domain.WorkTypes;

namespace PerformanceOs.Domain.Repositories;

/// <summary>
/// 作業タイプの永続化（docs/05-domain-design.md §7）。
/// </summary>
/// <remarks>
/// <b>書き込みメソッドは呼び出し時点で永続化まで行う。</b>
/// MVP の書き込み操作はすべて単一集約で完結するため、UnitOfWork を導入しない。
/// 複数集約にまたがる原子性が必要になった時点で見直す。
/// </remarks>
public interface IWorkTypeRepository
{
    Task<IReadOnlyList<WorkType>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<WorkType?> GetByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// 同名の作業タイプが存在するか。大文字小文字を区別しない（WT-2）。
    /// </summary>
    /// <param name="excludeId">
    /// 判定から除外する ID。改名時に自分自身を除外するために使う。
    /// これがないと、表示順だけを変える更新が一意制約違反になる。
    /// </param>
    Task<bool> ExistsByNameAsync(string name, long? excludeId, CancellationToken cancellationToken);

    Task AddAsync(WorkType workType, CancellationToken cancellationToken);

    Task UpdateAsync(WorkType workType, CancellationToken cancellationToken);
}
