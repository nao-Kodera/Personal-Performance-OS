using PerformanceOs.Application.Common;
using PerformanceOs.Application.TaskItems;
using PerformanceOs.Application.Tests.Fakes;
using PerformanceOs.Domain.Common;

namespace PerformanceOs.Application.Tests.TaskItems;

public class TaskItemServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);

    private readonly InMemoryTaskItemRepository _taskItems = new();
    private readonly InMemoryWorkTypeRepository _workTypes = new();
    private readonly InMemoryWorkSessionRepository _workSessions = new();
    private readonly StubTaskItemQuery _query = new();
    private readonly TaskItemService _service;

    private readonly long _designWorkTypeId;
    private readonly long _inactiveWorkTypeId;

    public TaskItemServiceTests()
    {
        _designWorkTypeId = _workTypes.Seed("設計", 20).Id;
        _inactiveWorkTypeId = _workTypes.Seed("旧分類", 90, isActive: false).Id;

        _service = new TaskItemService(
            _taskItems, _query, _workTypes, _workSessions, new FixedClock(Now));
    }

    private static CancellationToken Ct => CancellationToken.None;

    // ------------------------------------------------------------------
    // 作成
    // ------------------------------------------------------------------

    [Fact]
    public async Task 作成できる()
    {
        var created = await _service.CreateAsync("認証方式の検討", _designWorkTypeId, "メモ", Ct);

        Assert.Equal("認証方式の検討", created.Title);
        Assert.Equal(_designWorkTypeId, created.DefaultWorkTypeId);
        Assert.Equal("メモ", created.Note);
        Assert.False(created.IsArchived);
        Assert.True(created.Id > 0);
    }

    /// <summary>TI-2: 存在しない作業タイプは指定できない。</summary>
    [Fact]
    public async Task 存在しない作業タイプでは作成できない()
    {
        await Assert.ThrowsAsync<DomainRuleException>(
            () => _service.CreateAsync("認証方式の検討", 999, null, Ct));
    }

    [Fact]
    public async Task 無効な作業タイプでは作成できない()
    {
        await Assert.ThrowsAsync<DomainRuleException>(
            () => _service.CreateAsync("認証方式の検討", _inactiveWorkTypeId, null, Ct));
    }

    /// <summary>
    /// TI-3: 同名のタスクを複数作れる。同じ名前の作業を別の機会に行うのは正常。
    /// </summary>
    [Fact]
    public async Task 同名のタスクを複数作成できる()
    {
        var first = await _service.CreateAsync("週次レビュー", _designWorkTypeId, null, Ct);
        var second = await _service.CreateAsync("週次レビュー", _designWorkTypeId, null, Ct);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, _taskItems.Items.Count);
    }

    [Fact]
    public async Task 空のタイトルはドメイン例外になる()
    {
        await Assert.ThrowsAsync<DomainException>(
            () => _service.CreateAsync("   ", _designWorkTypeId, null, Ct));
    }

    /// <summary>
    /// 作業タイプの検証はタイトル検証より前に行われる。どちらが先でも
    /// 結果は変わらないが、順序が変わると例外の型が変わるため固定する。
    /// </summary>
    [Fact]
    public async Task 作業タイプが不正ならタイトル検証より先に弾かれる()
    {
        await Assert.ThrowsAsync<DomainRuleException>(
            () => _service.CreateAsync("   ", 999, null, Ct));
    }

    // ------------------------------------------------------------------
    // 更新
    // ------------------------------------------------------------------

    [Fact]
    public async Task 存在しないIDの更新は見つからない扱いになる()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.UpdateAsync(999, "タイトル", _designWorkTypeId, null, Ct));
    }

    [Fact]
    public async Task 更新できる()
    {
        var seeded = _taskItems.Seed("認証方式の検討", _designWorkTypeId);

        var updated = await _service.UpdateAsync(seeded.Id, "認証方式の再検討", _designWorkTypeId, "方針変更", Ct);

        Assert.Equal("認証方式の再検討", updated.Title);
        Assert.Equal("方針変更", updated.Note);
        Assert.Equal(Now, updated.UpdatedAt);
    }

    [Fact]
    public async Task 無効な作業タイプには更新できない()
    {
        var seeded = _taskItems.Seed("認証方式の検討", _designWorkTypeId);

        await Assert.ThrowsAsync<DomainRuleException>(
            () => _service.UpdateAsync(seeded.Id, "認証方式の検討", _inactiveWorkTypeId, null, Ct));
    }

    // ------------------------------------------------------------------
    // アーカイブ
    // ------------------------------------------------------------------

    [Fact]
    public async Task アーカイブできる()
    {
        var seeded = _taskItems.Seed("認証方式の検討", _designWorkTypeId);

        var archived = await _service.ArchiveAsync(seeded.Id, Ct);

        Assert.True(archived.IsArchived);
    }

    /// <summary>docs/07-api-design.md §2.7</summary>
    [Fact]
    public async Task 進行中のセッションがあるタスクはアーカイブできない()
    {
        var seeded = _taskItems.Seed("認証方式の検討", _designWorkTypeId);
        _workSessions.SeedActive(seeded.Id, _designWorkTypeId, Now);

        await Assert.ThrowsAsync<ConflictException>(() => _service.ArchiveAsync(seeded.Id, Ct));
    }

    /// <summary>
    /// 進行中セッションが「別のタスク」のものなら、アーカイブは妨げられない。
    /// 進行中の有無だけで判定していないことの確認。
    /// </summary>
    [Fact]
    public async Task 別のタスクが進行中でもアーカイブできる()
    {
        var target = _taskItems.Seed("認証方式の検討", _designWorkTypeId);
        var other = _taskItems.Seed("別の作業", _designWorkTypeId);
        _workSessions.SeedActive(other.Id, _designWorkTypeId, Now);

        var archived = await _service.ArchiveAsync(target.Id, Ct);

        Assert.True(archived.IsArchived);
    }

    [Fact]
    public async Task 存在しないIDのアーカイブは見つからない扱いになる()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ArchiveAsync(999, Ct));
    }

    [Fact]
    public async Task アーカイブを解除できる()
    {
        var seeded = _taskItems.Seed("認証方式の検討", _designWorkTypeId, isArchived: true);

        var unarchived = await _service.UnarchiveAsync(seeded.Id, Ct);

        Assert.False(unarchived.IsArchived);
    }

    // ------------------------------------------------------------------
    // 一覧
    // ------------------------------------------------------------------

    [Fact]
    public async Task 一覧は読み取りモデルに委譲される()
    {
        await _service.GetAsync(includeArchived: true, "認証", TaskItemSort.Recent, Ct);

        Assert.True(_query.LastIncludeArchived);
        Assert.Equal("認証", _query.LastKeyword);
        Assert.Equal(TaskItemSort.Recent, _query.LastSort);
    }
}
