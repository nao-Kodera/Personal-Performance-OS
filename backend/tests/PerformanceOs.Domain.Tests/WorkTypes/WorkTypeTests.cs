using PerformanceOs.Domain.Common;
using PerformanceOs.Domain.WorkTypes;

namespace PerformanceOs.Domain.Tests.WorkTypes;

public class WorkTypeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddHours(1);

    private static WorkType Create(string name = "実装", int displayOrder = 10)
        => WorkType.Create(name, displayOrder, Now);

    [Fact]
    public void 生成時は有効状態になる()
    {
        var workType = Create();

        Assert.Equal("実装", workType.Name);
        Assert.Equal(10, workType.DisplayOrder);
        Assert.True(workType.IsActive);
        Assert.Equal(Now, workType.CreatedAt);
        Assert.Equal(Now, workType.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void 空白のみの名称は例外になる(string name)
    {
        Assert.Throws<DomainException>(() => Create(name));
    }

    [Fact]
    public void 名称は五十文字まで許される()
    {
        var name = new string('あ', WorkType.MaxNameLength);

        Assert.Equal(name, Create(name).Name);
    }

    [Fact]
    public void 名称が五十文字を超えると例外になる()
    {
        var name = new string('あ', WorkType.MaxNameLength + 1);

        Assert.Throws<DomainException>(() => Create(name));
    }

    [Fact]
    public void 名称は前後の空白が除去される()
    {
        Assert.Equal("実装", Create("  実装  ").Name);
    }

    [Fact]
    public void 改名すると更新日時が変わる()
    {
        var workType = Create();

        workType.Rename("実装作業", Later);

        Assert.Equal("実装作業", workType.Name);
        Assert.Equal(Later, workType.UpdatedAt);
        Assert.Equal(Now, workType.CreatedAt);
    }

    [Fact]
    public void 同じ名称への改名では更新日時が変わらない()
    {
        var workType = Create();

        workType.Rename("実装", Later);

        Assert.Equal(Now, workType.UpdatedAt);
    }

    [Fact]
    public void 無効化できる()
    {
        var workType = Create();

        workType.Deactivate(Later);

        Assert.False(workType.IsActive);
        Assert.Equal(Later, workType.UpdatedAt);
    }

    [Fact]
    public void 無効化は冪等である()
    {
        var workType = Create();
        workType.Deactivate(Later);

        workType.Deactivate(Later.AddHours(1));

        Assert.False(workType.IsActive);
        Assert.Equal(Later, workType.UpdatedAt);
    }

    [Fact]
    public void 再有効化できる()
    {
        var workType = Create();
        workType.Deactivate(Later);

        workType.Activate(Later.AddHours(1));

        Assert.True(workType.IsActive);
        Assert.Equal(Later.AddHours(1), workType.UpdatedAt);
    }

    [Fact]
    public void 表示順を変更できる()
    {
        var workType = Create();

        workType.ChangeDisplayOrder(90, Later);

        Assert.Equal(90, workType.DisplayOrder);
        Assert.Equal(Later, workType.UpdatedAt);
    }

    /// <summary>
    /// docs/05-domain-design.md §4.1 WT-3。
    /// 削除すると過去の WorkSession の分類が失われ、分析 A-01 / A-02 が破綻する。
    /// </summary>
    [Fact]
    public void 削除する手段を持たない()
    {
        var members = typeof(WorkType).GetMembers().Select(m => m.Name);

        Assert.DoesNotContain(members, name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, name => name.Contains("Remove", StringComparison.OrdinalIgnoreCase));
    }
}
