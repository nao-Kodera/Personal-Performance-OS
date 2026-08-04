using PerformanceOs.Application.Common;
using PerformanceOs.Application.Tests.Fakes;
using PerformanceOs.Application.WorkTypes;
using PerformanceOs.Domain.Common;

namespace PerformanceOs.Application.Tests.WorkTypes;

public class WorkTypeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);

    private readonly InMemoryWorkTypeRepository _workTypes = new();
    private readonly WorkTypeService _service;

    public WorkTypeServiceTests()
    {
        _service = new WorkTypeService(_workTypes, new FixedClock(Now));
    }

    private static CancellationToken Ct => CancellationToken.None;

    // ------------------------------------------------------------------
    // 取得
    // ------------------------------------------------------------------

    [Fact]
    public async Task 既定では無効な作業タイプを含めない()
    {
        _workTypes.Seed("実装", 10);
        _workTypes.Seed("旧分類", 20, isActive: false);

        var result = await _service.GetAsync(includeInactive: false, Ct);

        Assert.Single(result);
        Assert.Equal("実装", result[0].Name);
    }

    [Fact]
    public async Task 無効なものも含めて取得できる()
    {
        _workTypes.Seed("実装", 10);
        _workTypes.Seed("旧分類", 20, isActive: false);

        var result = await _service.GetAsync(includeInactive: true, Ct);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task 表示順の昇順で返る()
    {
        _workTypes.Seed("その他", 90);
        _workTypes.Seed("実装", 10);
        _workTypes.Seed("設計", 20);

        var result = await _service.GetAsync(includeInactive: false, Ct);

        Assert.Equal(["実装", "設計", "その他"], result.Select(x => x.Name));
    }

    // ------------------------------------------------------------------
    // 作成
    // ------------------------------------------------------------------

    [Fact]
    public async Task 作成できる()
    {
        var created = await _service.CreateAsync("レビュー", displayOrder: 60, Ct);

        Assert.Equal("レビュー", created.Name);
        Assert.Equal(60, created.DisplayOrder);
        Assert.True(created.IsActive);
        Assert.True(created.Id > 0);
    }

    /// <summary>docs/07-api-design.md §2.2: 省略時は既存の最大値 + 10。</summary>
    [Fact]
    public async Task 表示順を省略すると既存の最大値に十を足した値になる()
    {
        _workTypes.Seed("実装", 10);
        _workTypes.Seed("その他", 90);

        var created = await _service.CreateAsync("レビュー", displayOrder: null, Ct);

        Assert.Equal(100, created.DisplayOrder);
    }

    [Fact]
    public async Task 一件も無い状態で表示順を省略すると十になる()
    {
        var created = await _service.CreateAsync("実装", displayOrder: null, Ct);

        Assert.Equal(10, created.DisplayOrder);
    }

    [Fact]
    public async Task 無効な作業タイプも表示順の計算に含める()
    {
        _workTypes.Seed("旧分類", 90, isActive: false);

        var created = await _service.CreateAsync("レビュー", displayOrder: null, Ct);

        Assert.Equal(100, created.DisplayOrder);
    }

    /// <summary>WT-2</summary>
    [Fact]
    public async Task 同名の作業タイプは作成できない()
    {
        _workTypes.Seed("実装", 10);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.CreateAsync("実装", displayOrder: 20, Ct));
    }

    [Fact]
    public async Task 大文字小文字違いの同名も作成できない()
    {
        _workTypes.Seed("Review", 10);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.CreateAsync("review", displayOrder: 20, Ct));
    }

    [Fact]
    public async Task 前後の空白違いの同名も作成できない()
    {
        _workTypes.Seed("実装", 10);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.CreateAsync("  実装  ", displayOrder: 20, Ct));
    }

    [Fact]
    public async Task 空の名称はドメイン例外になる()
    {
        await Assert.ThrowsAsync<DomainException>(
            () => _service.CreateAsync("   ", displayOrder: 10, Ct));
    }

    // ------------------------------------------------------------------
    // 更新
    // ------------------------------------------------------------------

    [Fact]
    public async Task 存在しないIDの更新は見つからない扱いになる()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.UpdateAsync(999, "実装", 10, true, Ct));
    }

    [Fact]
    public async Task 改名できる()
    {
        var seeded = _workTypes.Seed("実装", 10);

        var updated = await _service.UpdateAsync(seeded.Id, "実装作業", 10, true, Ct);

        Assert.Equal("実装作業", updated.Name);
        Assert.Equal(Now, updated.UpdatedAt);
    }

    /// <summary>
    /// ExistsByNameAsync に excludeId が無いと、この操作が誤って
    /// 409 になる。設計書 §7 の署名に対する修正の根拠。
    /// </summary>
    [Fact]
    public async Task 名称を変えずに表示順だけ更新できる()
    {
        var seeded = _workTypes.Seed("実装", 10);

        var updated = await _service.UpdateAsync(seeded.Id, "実装", 15, true, Ct);

        Assert.Equal(15, updated.DisplayOrder);
    }

    [Fact]
    public async Task 他の作業タイプが使っている名称には変更できない()
    {
        _workTypes.Seed("実装", 10);
        var target = _workTypes.Seed("設計", 20);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.UpdateAsync(target.Id, "実装", 20, true, Ct));
    }

    [Fact]
    public async Task 無効化できる()
    {
        var seeded = _workTypes.Seed("実装", 10);

        var updated = await _service.UpdateAsync(seeded.Id, "実装", 10, isActive: false, Ct);

        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task 再有効化できる()
    {
        var seeded = _workTypes.Seed("旧分類", 10, isActive: false);

        var updated = await _service.UpdateAsync(seeded.Id, "旧分類", 10, isActive: true, Ct);

        Assert.True(updated.IsActive);
    }

    // ------------------------------------------------------------------
    // 単体取得
    // ------------------------------------------------------------------

    [Fact]
    public async Task 存在しないIDの取得は見つからない扱いになる()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(999, Ct));
    }
}
