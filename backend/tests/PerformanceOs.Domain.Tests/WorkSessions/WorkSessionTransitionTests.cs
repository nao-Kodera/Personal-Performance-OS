using PerformanceOs.Domain.Common;
using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Domain.Tests.WorkSessions;

/// <summary>
/// 状態遷移と、状態に対する PerformanceResult の整合（WS-2 / WS-3 / WS-4 / WS-7）。
/// </summary>
public class WorkSessionTransitionTests
{
    // ------------------------------------------------------------------
    // Start
    // ------------------------------------------------------------------

    [Fact]
    public void 開始直後は進行中で成果を持たない()
    {
        var session = WorkSessionFactory.Started();

        Assert.Equal(SessionStatus.InProgress, session.Status);
        Assert.Equal(WorkSessionFactory.StartedAt, session.StartedAt);
        Assert.Null(session.FinishedAt);   // WS-2
        Assert.Null(session.Result);       // WS-2
        Assert.Equal(0, session.InterruptionCount);
    }

    /// <summary>WS-1: PreWorkState と WorkContext は開始と同時に必ず生成される。</summary>
    [Fact]
    public void 開始時に作業前状態と作業環境が生成される()
    {
        var session = WorkSessionFactory.Started();

        Assert.NotNull(session.PreWorkState);
        Assert.NotNull(session.WorkContext);
    }

    /// <summary>
    /// PS-3: 作業前状態の記録時刻は開始時刻と同一である。
    /// 呼び出し側が別の時刻を渡せないよう、集約が設定している。
    /// </summary>
    [Fact]
    public void 作業前状態と作業環境の記録時刻は開始時刻と一致する()
    {
        var session = WorkSessionFactory.Started();

        Assert.Equal(session.StartedAt, session.PreWorkState.RecordedAt);
        Assert.Equal(session.StartedAt, session.WorkContext.RecordedAt);
    }

    // ------------------------------------------------------------------
    // Finish
    // ------------------------------------------------------------------

    /// <summary>WS-3: Completed なら PerformanceResult が必ず存在する。</summary>
    [Fact]
    public void 終了すると完了になり成果が必ず存在する()
    {
        var session = WorkSessionFactory.Started();

        session.Finish(1, WorkSessionFactory.Result(), WorkSessionFactory.FinishedAt);

        Assert.Equal(SessionStatus.Completed, session.Status);
        Assert.Equal(WorkSessionFactory.FinishedAt, session.FinishedAt);
        Assert.NotNull(session.Result);
        Assert.Equal(1, session.InterruptionCount);
    }

    [Fact]
    public void 成果の記録時刻は終了時刻と一致する()
    {
        var session = WorkSessionFactory.Completed();

        Assert.Equal(session.FinishedAt, session.Result!.RecordedAt);
        Assert.False(session.Result.IsEdited);
    }

    /// <summary>docs/08-technical-design.md §6.2 T-03。</summary>
    [Fact]
    public void 完了済みのセッションは再度終了できない()
    {
        var session = WorkSessionFactory.Completed();

        Assert.Throws<DomainException>(
            () => session.Finish(0, WorkSessionFactory.Result(), WorkSessionFactory.FinishedAt.AddHours(1)));
    }

    /// <summary>docs/08-technical-design.md §6.2 T-04。</summary>
    [Fact]
    public void 中断終了したセッションは終了できない()
    {
        var session = WorkSessionFactory.Abandoned();

        Assert.Throws<DomainException>(
            () => session.Finish(0, WorkSessionFactory.Result(), WorkSessionFactory.FinishedAt.AddHours(1)));
    }

    /// <summary>WS-5: 終了時刻は開始時刻より後でなければならない。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-3600)]
    public void 終了時刻が開始時刻以前なら例外になる(int offsetSeconds)
    {
        var session = WorkSessionFactory.Started();
        var finishedAt = WorkSessionFactory.StartedAt.AddSeconds(offsetSeconds);

        Assert.Throws<DomainException>(
            () => session.Finish(0, WorkSessionFactory.Result(), finishedAt));
    }

    [Fact]
    public void 終了に失敗しても進行中のままである()
    {
        var session = WorkSessionFactory.Started();

        Assert.Throws<DomainException>(
            () => session.Finish(-1, WorkSessionFactory.Result(), WorkSessionFactory.FinishedAt));

        Assert.Equal(SessionStatus.InProgress, session.Status);
        Assert.Null(session.FinishedAt);
        Assert.Null(session.Result);
    }

    /// <summary>WS-6</summary>
    [Fact]
    public void 中断回数が負なら例外になる()
    {
        var session = WorkSessionFactory.Started();

        Assert.Throws<DomainException>(
            () => session.Finish(-1, WorkSessionFactory.Result(), WorkSessionFactory.FinishedAt));
    }

    // ------------------------------------------------------------------
    // Abandon
    // ------------------------------------------------------------------

    /// <summary>WS-4: Abandoned なら PerformanceResult は存在しない。</summary>
    [Fact]
    public void 中断終了すると成果を持たない()
    {
        var session = WorkSessionFactory.Started();

        session.Abandon("会議に呼ばれて中断", WorkSessionFactory.FinishedAt);

        Assert.Equal(SessionStatus.Abandoned, session.Status);
        Assert.Equal(WorkSessionFactory.FinishedAt, session.FinishedAt);
        Assert.Null(session.Result);
        Assert.Equal("会議に呼ばれて中断", session.AbandonNote);
    }

    [Fact]
    public void 完了済みのセッションは中断終了できない()
    {
        var session = WorkSessionFactory.Completed();

        Assert.Throws<DomainException>(() => session.Abandon(null, WorkSessionFactory.FinishedAt.AddHours(1)));
    }

    /// <summary>WS-7: 終端状態から他の状態へ遷移しない。</summary>
    [Fact]
    public void 中断終了したセッションは再度中断終了できない()
    {
        var session = WorkSessionFactory.Abandoned();

        Assert.Throws<DomainException>(() => session.Abandon(null, WorkSessionFactory.FinishedAt.AddHours(1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 中断終了のメモが空白のみならnullになる(string? note)
    {
        var session = WorkSessionFactory.Started();

        session.Abandon(note, WorkSessionFactory.FinishedAt);

        Assert.Null(session.AbandonNote);
    }

    [Fact]
    public void 中断終了のメモが上限を超えると例外になる()
    {
        var session = WorkSessionFactory.Started();
        var note = new string('あ', WorkSession.MaxAbandonNoteLength + 1);

        Assert.Throws<DomainException>(() => session.Abandon(note, WorkSessionFactory.FinishedAt));
    }

    // ------------------------------------------------------------------
    // 訂正
    // ------------------------------------------------------------------

    [Fact]
    public void 完了後に成果を訂正できる()
    {
        var session = WorkSessionFactory.Completed();
        var editedAt = WorkSessionFactory.FinishedAt.AddDays(1);

        session.UpdateResult(WorkSessionFactory.Result(focus: 2, note: "後から見ると集中していなかった"), editedAt);

        Assert.Equal(2, session.Result!.FocusLevel.Value);
        Assert.Equal("後から見ると集中していなかった", session.Result.Note);
    }

    /// <summary>
    /// PR-2: 訂正しても初回記録時刻は変わらない。
    /// RecordedAt と UpdatedAt の差で事後編集を識別できるようにするため。
    /// </summary>
    [Fact]
    public void 成果を訂正しても初回記録時刻は変わらない()
    {
        var session = WorkSessionFactory.Completed();
        var editedAt = WorkSessionFactory.FinishedAt.AddDays(1);

        session.UpdateResult(WorkSessionFactory.Result(focus: 2), editedAt);

        Assert.Equal(WorkSessionFactory.FinishedAt, session.Result!.RecordedAt);
        Assert.Equal(editedAt, session.Result.UpdatedAt);
        Assert.True(session.Result.IsEdited);
    }

    [Fact]
    public void 進行中のセッションの成果は訂正できない()
    {
        var session = WorkSessionFactory.Started();

        Assert.Throws<DomainException>(
            () => session.UpdateResult(WorkSessionFactory.Result(), WorkSessionFactory.FinishedAt));
    }

    [Fact]
    public void 中断終了したセッションの成果は訂正できない()
    {
        var session = WorkSessionFactory.Abandoned();

        Assert.Throws<DomainException>(
            () => session.UpdateResult(WorkSessionFactory.Result(), WorkSessionFactory.FinishedAt));
    }

    [Fact]
    public void 中断回数を訂正できる()
    {
        var session = WorkSessionFactory.Completed(interruptionCount: 1);
        var editedAt = WorkSessionFactory.FinishedAt.AddDays(1);

        session.UpdateInterruptionCount(4, editedAt);

        Assert.Equal(4, session.InterruptionCount);
        Assert.Equal(editedAt, session.UpdatedAt);
    }

    [Fact]
    public void 同じ中断回数への訂正では更新日時が変わらない()
    {
        var session = WorkSessionFactory.Completed(interruptionCount: 1);

        session.UpdateInterruptionCount(1, WorkSessionFactory.FinishedAt.AddDays(1));

        Assert.Equal(WorkSessionFactory.FinishedAt, session.UpdatedAt);
    }

    [Fact]
    public void 中断回数を負の値に訂正できない()
    {
        var session = WorkSessionFactory.Completed();

        Assert.Throws<DomainException>(
            () => session.UpdateInterruptionCount(-1, WorkSessionFactory.FinishedAt.AddDays(1)));
    }
}
