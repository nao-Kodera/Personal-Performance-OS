using PerformanceOs.Application.Common;
using PerformanceOs.Domain.Repositories;
using PerformanceOs.Domain.Time;
using PerformanceOs.Domain.WorkTypes;

namespace PerformanceOs.Application.WorkTypes;

/// <summary>
/// 作業タイプのユースケース。集約ごとに 1 クラスとし、
/// ユースケースごとにクラスを作らない（docs/08-technical-design.md §2.3）。
/// </summary>
public sealed class WorkTypeService
{
    /// <summary>表示順の既定間隔。後から間に挿入できるよう 10 刻みにする。</summary>
    private const int DisplayOrderStep = 10;

    private readonly IWorkTypeRepository _workTypes;
    private readonly IClock _clock;

    public WorkTypeService(IWorkTypeRepository workTypes, IClock clock)
    {
        _workTypes = workTypes;
        _clock = clock;
    }

    public Task<IReadOnlyList<WorkType>> GetAsync(
        bool includeInactive, CancellationToken cancellationToken)
        => _workTypes.GetAllAsync(includeInactive, cancellationToken);

    public async Task<WorkType> GetByIdAsync(long id, CancellationToken cancellationToken)
        => await _workTypes.GetByIdAsync(id, cancellationToken)
           ?? throw NotFoundException.For("作業タイプ", id);

    /// <param name="displayOrder">省略時は既存の最大値 + 10（docs/07-api-design.md §2.2）。</param>
    public async Task<WorkType> CreateAsync(
        string name, int? displayOrder, CancellationToken cancellationToken)
    {
        await EnsureNameIsAvailableAsync(name, excludeId: null, cancellationToken);

        var order = displayOrder ?? await NextDisplayOrderAsync(cancellationToken);

        // 名称の検証はドメイン側が行う。ここでは重複だけを見る。
        var workType = WorkType.Create(name, order, _clock.UtcNow);

        await _workTypes.AddAsync(workType, cancellationToken);

        return workType;
    }

    public async Task<WorkType> UpdateAsync(
        long id, string name, int displayOrder, bool isActive, CancellationToken cancellationToken)
    {
        var workType = await GetByIdAsync(id, cancellationToken);

        // 自分自身は重複判定から除外する。これがないと、名称を変えずに
        // 表示順だけを更新する操作が一意制約違反になる。
        await EnsureNameIsAvailableAsync(name, excludeId: id, cancellationToken);

        var now = _clock.UtcNow;

        workType.Rename(name, now);
        workType.ChangeDisplayOrder(displayOrder, now);

        if (isActive)
        {
            workType.Activate(now);
        }
        else
        {
            workType.Deactivate(now);
        }

        await _workTypes.UpdateAsync(workType, cancellationToken);

        return workType;
    }

    /// <summary>
    /// 同名の作業タイプが無いことを確認する（WT-2）。
    /// </summary>
    /// <remarks>
    /// このチェックだけでは並行リクエストを防げない。DB の
    /// <c>uq_work_types_name</c> が最終的な担保であり、ここでの検査は
    /// 分かりやすいエラーを返すためのものである。
    /// </remarks>
    private async Task EnsureNameIsAvailableAsync(
        string name, long? excludeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            // 空文字の検証はドメイン側に任せる。ここで問い合わせても意味がない。
            return;
        }

        if (await _workTypes.ExistsByNameAsync(name, excludeId, cancellationToken))
        {
            throw new ConflictException($"同名の作業タイプが既に存在します: {name.Trim()}");
        }
    }

    private async Task<int> NextDisplayOrderAsync(CancellationToken cancellationToken)
    {
        var all = await _workTypes.GetAllAsync(includeInactive: true, cancellationToken);

        return all.Count == 0
            ? DisplayOrderStep
            : all.Max(x => x.DisplayOrder) + DisplayOrderStep;
    }
}
