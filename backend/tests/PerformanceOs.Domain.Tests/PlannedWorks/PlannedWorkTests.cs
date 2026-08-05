using PerformanceOs.Domain.Common;
using PerformanceOs.Domain.PlannedWorks;
using PerformanceOs.Domain.Time;

namespace PerformanceOs.Domain.Tests.PlannedWorks;

public class PlannedWorkTests
{
    private static readonly DateOnly Today = new(2026, 8, 5);
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 0, 0, 0, TimeSpan.Zero);

    private static PlannedWork Plan(TimeBand? timeBand = null, int? minutes = null)
        => PlannedWork.Plan(Today, taskItemId: 1, workTypeId: 2, timeBand, minutes, Now);

    [Fact]
    public void 予定した値を保持する()
    {
        var planned = Plan(TimeBand.Morning, 90);

        Assert.Equal(Today, planned.TargetDate);
        Assert.Equal(1, planned.TaskItemId);
        Assert.Equal(2, planned.WorkTypeId);
        Assert.Equal(TimeBand.Morning, planned.PlannedTimeBand);
        Assert.Equal(90, planned.PlannedMinutes);
    }

    [Fact]
    public void 時間帯と所要時間は任意である()
    {
        var planned = Plan();

        Assert.Null(planned.PlannedTimeBand);
        Assert.Null(planned.PlannedMinutes);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(1455)]
    [InlineData(100)]
    public void 所要時間が範囲外または15分単位でなければ拒む(int minutes)
    {
        Assert.Throws<DomainException>(() => Plan(minutes: minutes));
    }

    [Fact]
    public void 定義されていない時間帯を拒む()
    {
        Assert.Throws<DomainException>(() => Plan(timeBand: (TimeBand)99));
    }

    /// <summary>
    /// PW-3。同じタスクを 1 日に複数回予定するのは正常な操作である。
    /// 実行率の分母は予定の件数であり、タスクの件数ではない。
    /// </summary>
    [Fact]
    public void 同一タスク同一日の予定を複数作れる()
    {
        var first = Plan(TimeBand.Morning);
        var second = Plan(TimeBand.Afternoon);

        Assert.Equal(first.TargetDate, second.TargetDate);
        Assert.Equal(first.TaskItemId, second.TaskItemId);
    }

    [Fact]
    public void 予定した直後は未実行ではない()
    {
        var planned = Plan();

        Assert.Null(planned.NonExecution);
        Assert.False(planned.IsUnexecuted);
    }

    [Fact]
    public void 未実行を記録できる()
    {
        var planned = Plan();

        planned.RecordNonExecution(NonExecutionReason.Overplanned, "  詰め込みすぎた  ", Now);

        Assert.True(planned.IsUnexecuted);
        Assert.Equal(NonExecutionReason.Overplanned, planned.NonExecution?.Reason);
        Assert.Equal("詰め込みすぎた", planned.NonExecution?.Note);
        Assert.Equal(Now, planned.NonExecution?.RecordedAt);
    }

    /// <summary>
    /// NE-1。1 つの予定に対して最大 1 件。2 度目は新規作成ではなく訂正になる。
    /// NE-3 により初回記録時刻は変わらない。
    /// </summary>
    [Fact]
    public void 未実行を訂正しても初回記録時刻は変わらない()
    {
        var planned = Plan();
        var later = Now.AddHours(5);

        planned.RecordNonExecution(NonExecutionReason.NoTime, null, Now);
        var first = planned.NonExecution;

        planned.RecordNonExecution(NonExecutionReason.Overplanned, "計画が過大だった", later);

        Assert.Same(first, planned.NonExecution);
        Assert.Equal(NonExecutionReason.Overplanned, planned.NonExecution?.Reason);
        Assert.Equal(Now, planned.NonExecution?.RecordedAt);
        Assert.Equal(later, planned.NonExecution?.UpdatedAt);
    }

    [Fact]
    public void 未実行の理由が不正なら拒む()
    {
        var planned = Plan();

        Assert.Throws<DomainException>(
            () => planned.RecordNonExecution((NonExecutionReason)99, null, Now));
    }

    [Fact]
    public void 未実行のメモが上限を超えたら拒む()
    {
        var planned = Plan();
        var tooLong = new string('あ', NonExecutionRecord.MaxNoteLength + 1);

        Assert.Throws<DomainException>(
            () => planned.RecordNonExecution(NonExecutionReason.Other, tooLong, Now));
    }

    /// <summary>
    /// 計画の妥当性を検証するための区分が揃っていること
    /// （docs/02-glossary.md §1・docs/01-product-requirements.md §2 P4）。
    /// </summary>
    [Fact]
    public void 未実行の理由は6区分である()
    {
        var reasons = Enum.GetValues<NonExecutionReason>();

        Assert.Equal(
            [
                NonExecutionReason.NoTime,
                NonExecutionReason.Interrupted,
                NonExecutionReason.PoorCondition,
                NonExecutionReason.Deprioritized,
                NonExecutionReason.Overplanned,
                NonExecutionReason.Other,
            ],
            reasons);
    }
}
