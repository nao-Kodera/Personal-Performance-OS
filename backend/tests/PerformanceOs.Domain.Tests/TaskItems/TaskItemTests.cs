using PerformanceOs.Domain.Common;
using PerformanceOs.Domain.TaskItems;

namespace PerformanceOs.Domain.Tests.TaskItems;

public class TaskItemTests
{
    private const long WorkTypeId = 2;

    private static readonly DateTimeOffset Now = new(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddHours(1);

    private static TaskItem Create(
        string title = "認証方式の検討",
        long defaultWorkTypeId = WorkTypeId,
        string? note = null)
        => TaskItem.Create(title, defaultWorkTypeId, note, Now);

    [Fact]
    public void 生成時は未アーカイブになる()
    {
        var task = Create();

        Assert.Equal("認証方式の検討", task.Title);
        Assert.Equal(WorkTypeId, task.DefaultWorkTypeId);
        Assert.Null(task.Note);
        Assert.False(task.IsArchived);
        Assert.Equal(Now, task.CreatedAt);
        Assert.Equal(Now, task.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void 空白のみのタイトルは例外になる(string title)
    {
        Assert.Throws<DomainException>(() => Create(title));
    }

    [Fact]
    public void タイトルは二百文字まで許される()
    {
        var title = new string('あ', TaskItem.MaxTitleLength);

        Assert.Equal(title, Create(title).Title);
    }

    [Fact]
    public void タイトルが二百文字を超えると例外になる()
    {
        Assert.Throws<DomainException>(() => Create(new string('あ', TaskItem.MaxTitleLength + 1)));
    }

    [Fact]
    public void メモが二千文字を超えると例外になる()
    {
        Assert.Throws<DomainException>(
            () => Create(note: new string('あ', TaskItem.MaxNoteLength + 1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 空白のみのメモはnullになる(string? note)
    {
        Assert.Null(Create(note: note).Note);
    }

    [Fact]
    public void タイトルとメモは前後の空白が除去される()
    {
        var task = Create("  認証方式の検討  ", note: "  下書きあり  ");

        Assert.Equal("認証方式の検討", task.Title);
        Assert.Equal("下書きあり", task.Note);
    }

    /// <summary>
    /// docs/05-domain-design.md §4.2 TI-3。
    /// 同じ名前の作業を別の機会に行うのは正常であり、重複を禁止しない。
    /// </summary>
    [Fact]
    public void 同名のタスクを複数生成できる()
    {
        var first = Create();
        var second = Create();

        Assert.Equal(first.Title, second.Title);
    }

    [Fact]
    public void 更新すると更新日時が変わる()
    {
        var task = Create();

        task.Update("認証方式の再検討", 3, "方針変更", Later);

        Assert.Equal("認証方式の再検討", task.Title);
        Assert.Equal(3, task.DefaultWorkTypeId);
        Assert.Equal("方針変更", task.Note);
        Assert.Equal(Later, task.UpdatedAt);
    }

    [Fact]
    public void 同じ内容での更新では更新日時が変わらない()
    {
        var task = Create();

        task.Update("認証方式の検討", WorkTypeId, null, Later);

        Assert.Equal(Now, task.UpdatedAt);
    }

    [Fact]
    public void アーカイブできる()
    {
        var task = Create();

        task.Archive(Later);

        Assert.True(task.IsArchived);
        Assert.Equal(Later, task.UpdatedAt);
    }

    [Fact]
    public void アーカイブは冪等である()
    {
        var task = Create();
        task.Archive(Later);

        task.Archive(Later.AddHours(1));

        Assert.Equal(Later, task.UpdatedAt);
    }

    [Fact]
    public void アーカイブを解除できる()
    {
        var task = Create();
        task.Archive(Later);

        task.Unarchive(Later.AddHours(1));

        Assert.False(task.IsArchived);
    }

    /// <summary>
    /// docs/08-technical-design.md §8 の禁止事項 2。
    /// タスク管理アプリの標準機能は「あって当然」と判断されて追加されやすいため、
    /// テストで固定する。docs/05-domain-design.md §9 も参照。
    /// </summary>
    [Theory]
    [InlineData("Complete")]
    [InlineData("Done")]
    [InlineData("Finish")]
    [InlineData("Due")]
    [InlineData("Deadline")]
    [InlineData("Priority")]
    [InlineData("Tag")]
    [InlineData("Parent")]
    [InlineData("Recurrence")]
    [InlineData("Estimate")]
    [InlineData("Progress")]
    public void 消化を目的とする概念を持たない(string forbidden)
    {
        var members = typeof(TaskItem).GetMembers().Select(m => m.Name).ToList();

        Assert.DoesNotContain(
            members,
            name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
    }
}
