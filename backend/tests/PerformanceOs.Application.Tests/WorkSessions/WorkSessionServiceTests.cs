using PerformanceOs.Application.Common;
using PerformanceOs.Application.Tests.Fakes;
using PerformanceOs.Application.WorkSessions;
using PerformanceOs.Domain.ValueObjects;
using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Application.Tests.WorkSessions;

public class WorkSessionServiceTests
{
    /// <summary>UTC 2026-08-04 00:12 = JST 09:12（午前）。</summary>
    private static readonly DateTimeOffset StartedAt = new(2026, 8, 4, 0, 12, 0, TimeSpan.Zero);

    private readonly InMemoryWorkSessionRepository _sessions = new();
    private readonly InMemoryTaskItemRepository _taskItems = new();
    private readonly InMemoryWorkTypeRepository _workTypes = new();
    private readonly FixedClock _clock = new(StartedAt);
    private readonly WorkSessionService _service;

    private readonly long _taskId;
    private readonly long _archivedTaskId;
    private readonly long _workTypeId;
    private readonly long _inactiveWorkTypeId;

    public WorkSessionServiceTests()
    {
        _workTypeId = _workTypes.Seed("設計", 20).Id;
        _inactiveWorkTypeId = _workTypes.Seed("旧分類", 90, isActive: false).Id;
        _taskId = _taskItems.Seed("認証方式の検討", _workTypeId).Id;
        _archivedTaskId = _taskItems.Seed("終わった作業", _workTypeId, isArchived: true).Id;

        _service = new WorkSessionService(_sessions, _taskItems, _workTypes, _clock);
    }

    private static CancellationToken Ct => CancellationToken.None;

    private static PreWorkStateInput PreWorkState(int fatigue = 2)
        => new(new Rating(fatigue), new Rating(4), new Rating(4));

    private static WorkContextInput WorkContext()
        => new(WorkLocation.Home, null, 1, false);

    private static PerformanceResultInput Result(int focus = 4, int fatigueAfter = 4)
        => new(new Rating(focus), new Rating(4), new Rating(3), new Rating(4), new Rating(fatigueAfter), null);

    private Task<WorkSession> StartAsync(long? taskId = null, long? workTypeId = null)
        => _service.StartAsync(
            taskId ?? _taskId, workTypeId ?? _workTypeId, PreWorkState(), WorkContext(), Ct);

    // ------------------------------------------------------------------
    // 開始
    // ------------------------------------------------------------------

    [Fact]
    public async Task 開始できる()
    {
        var session = await StartAsync();

        Assert.Equal(SessionStatus.InProgress, session.Status);
        Assert.Equal(StartedAt, session.StartedAt);
        Assert.Equal(_taskId, session.TaskItemId);
        Assert.Equal(_workTypeId, session.WorkTypeId);
        Assert.NotNull(session.PreWorkState);
        Assert.NotNull(session.WorkContext);
        Assert.Null(session.Result);
    }

    /// <summary>
    /// docs/08-technical-design.md §6.2 T-01。
    /// 並行作業を記録すると、どちらの成果か分離できないため禁止する。
    /// </summary>
    [Fact]
    public async Task 進行中のセッションがあると開始できない()
    {
        await StartAsync();

        await Assert.ThrowsAsync<ConflictException>(() => StartAsync());
    }

    [Fact]
    public async Task 終了後は再び開始できる()
    {
        var first = await StartAsync();
        _clock.UtcNow = StartedAt.AddMinutes(93);
        await _service.FinishAsync(first.Id, 1, Result(), Ct);

        var second = await StartAsync();

        Assert.Equal(SessionStatus.InProgress, second.Status);
    }

    [Fact]
    public async Task 存在しないタスクでは開始できない()
    {
        await Assert.ThrowsAsync<DomainRuleException>(() => StartAsync(taskId: 999));
    }

    [Fact]
    public async Task アーカイブ済みのタスクでは開始できない()
    {
        await Assert.ThrowsAsync<DomainRuleException>(() => StartAsync(taskId: _archivedTaskId));
    }

    [Fact]
    public async Task 存在しない作業タイプでは開始できない()
    {
        await Assert.ThrowsAsync<DomainRuleException>(() => StartAsync(workTypeId: 999));
    }

    [Fact]
    public async Task 無効な作業タイプでは開始できない()
    {
        await Assert.ThrowsAsync<DomainRuleException>(() => StartAsync(workTypeId: _inactiveWorkTypeId));
    }

    [Fact]
    public async Task 開始に失敗するとセッションは作られない()
    {
        await Assert.ThrowsAsync<DomainRuleException>(() => StartAsync(taskId: 999));

        Assert.Empty(_sessions.Items);
        Assert.Null(await _service.GetActiveAsync(Ct));
    }

    // ------------------------------------------------------------------
    // 終了
    // ------------------------------------------------------------------

    /// <summary>docs/08-technical-design.md §6.2 T-06: Completed なら Result が必ず存在する。</summary>
    [Fact]
    public async Task 終了すると成果が保存される()
    {
        var session = await StartAsync();
        _clock.UtcNow = StartedAt.AddMinutes(93);

        var finished = await _service.FinishAsync(session.Id, 1, Result(), Ct);

        Assert.Equal(SessionStatus.Completed, finished.Session.Status);
        Assert.NotNull(finished.Session.Result);
        Assert.Equal(93, finished.Session.DurationMinutes);
        Assert.Equal(1, finished.Session.InterruptionCount);
        Assert.Empty(finished.Warnings);
    }

    [Fact]
    public async Task 疲労増加量が算出される()
    {
        var session = await _service.StartAsync(
            _taskId, _workTypeId, PreWorkState(fatigue: 2), WorkContext(), Ct);
        _clock.UtcNow = StartedAt.AddMinutes(93);

        var finished = await _service.FinishAsync(session.Id, 0, Result(fatigueAfter: 5), Ct);

        Assert.Equal(3, finished.Session.FatigueDelta);
    }

    [Fact]
    public async Task 存在しないセッションの終了は見つからない扱いになる()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.FinishAsync(999, 0, Result(), Ct));
    }

    /// <summary>docs/08-technical-design.md §6.2 T-03。</summary>
    [Fact]
    public async Task 完了済みのセッションは再度終了できない()
    {
        var session = await StartAsync();
        _clock.UtcNow = StartedAt.AddMinutes(93);
        await _service.FinishAsync(session.Id, 0, Result(), Ct);

        _clock.UtcNow = StartedAt.AddMinutes(120);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.FinishAsync(session.Id, 0, Result(), Ct));
    }

    /// <summary>docs/08-technical-design.md §6.2 T-04。</summary>
    [Fact]
    public async Task 中断終了したセッションは終了できない()
    {
        var session = await StartAsync();
        _clock.UtcNow = StartedAt.AddMinutes(8);
        await _service.AbandonAsync(session.Id, "会議に呼ばれた", Ct);

        _clock.UtcNow = StartedAt.AddMinutes(20);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.FinishAsync(session.Id, 0, Result(), Ct));
    }

    /// <summary>WS-5: サーバー時刻が巻き戻った場合。</summary>
    [Fact]
    public async Task 現在時刻が開始時刻以前なら終了できない()
    {
        var session = await StartAsync();
        _clock.UtcNow = StartedAt.AddSeconds(-1);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.FinishAsync(session.Id, 0, Result(), Ct));
    }

    // ------------------------------------------------------------------
    // 警告
    // ------------------------------------------------------------------

    [Fact]
    public async Task 八時間を超えると長時間の警告が出る()
    {
        var session = await StartAsync();
        _clock.UtcNow = StartedAt.AddMinutes(481);

        var finished = await _service.FinishAsync(session.Id, 0, Result(), Ct);

        Assert.Contains(SessionWarning.LongSession, finished.Warnings);
    }

    [Fact]
    public async Task 八時間ちょうどでは警告が出ない()
    {
        var session = await StartAsync();
        _clock.UtcNow = StartedAt.AddMinutes(480);

        var finished = await _service.FinishAsync(session.Id, 0, Result(), Ct);

        Assert.Empty(finished.Warnings);
    }

    [Fact]
    public async Task 一分未満では短時間の警告が出る()
    {
        var session = await StartAsync();
        _clock.UtcNow = StartedAt.AddSeconds(30);

        var finished = await _service.FinishAsync(session.Id, 0, Result(), Ct);

        Assert.Contains(SessionWarning.VeryShortSession, finished.Warnings);
    }

    [Fact]
    public async Task 警告が出ても保存は妨げられない()
    {
        var session = await StartAsync();
        _clock.UtcNow = StartedAt.AddSeconds(30);

        var finished = await _service.FinishAsync(session.Id, 0, Result(), Ct);

        Assert.Equal(SessionStatus.Completed, finished.Session.Status);
        Assert.NotNull(finished.Session.Result);
    }

    // ------------------------------------------------------------------
    // 中断終了
    // ------------------------------------------------------------------

    /// <summary>docs/08-technical-design.md §6.2 T-07: Abandoned に Result は無い。</summary>
    [Fact]
    public async Task 中断終了すると成果を持たない()
    {
        var session = await StartAsync();
        _clock.UtcNow = StartedAt.AddMinutes(8);

        var abandoned = await _service.AbandonAsync(session.Id, "会議に呼ばれた", Ct);

        Assert.Equal(SessionStatus.Abandoned, abandoned.Status);
        Assert.Null(abandoned.Result);
        Assert.Equal("会議に呼ばれた", abandoned.AbandonNote);
    }

    [Fact]
    public async Task 完了済みのセッションは中断終了できない()
    {
        var session = await StartAsync();
        _clock.UtcNow = StartedAt.AddMinutes(93);
        await _service.FinishAsync(session.Id, 0, Result(), Ct);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.AbandonAsync(session.Id, null, Ct));
    }

    [Fact]
    public async Task 中断終了後は再び開始できる()
    {
        var first = await StartAsync();
        _clock.UtcNow = StartedAt.AddMinutes(8);
        await _service.AbandonAsync(first.Id, null, Ct);

        var second = await StartAsync();

        Assert.Equal(SessionStatus.InProgress, second.Status);
    }

    // ------------------------------------------------------------------
    // 訂正
    // ------------------------------------------------------------------

    [Fact]
    public async Task 成果を訂正できる()
    {
        var session = await StartAsync();
        _clock.UtcNow = StartedAt.AddMinutes(93);
        await _service.FinishAsync(session.Id, 1, Result(focus: 4), Ct);

        _clock.UtcNow = StartedAt.AddDays(1);
        var updated = await _service.UpdateResultAsync(session.Id, 4, Result(focus: 2), Ct);

        Assert.Equal(2, updated.Result!.FocusLevel.Value);
        Assert.Equal(4, updated.InterruptionCount);
    }

    /// <summary>PR-2: 訂正しても初回記録時刻は変わらない。</summary>
    [Fact]
    public async Task 訂正しても初回記録時刻は変わらない()
    {
        var session = await StartAsync();
        var finishedAt = StartedAt.AddMinutes(93);
        _clock.UtcNow = finishedAt;
        await _service.FinishAsync(session.Id, 1, Result(), Ct);

        _clock.UtcNow = StartedAt.AddDays(1);
        var updated = await _service.UpdateResultAsync(session.Id, 1, Result(focus: 2), Ct);

        Assert.Equal(finishedAt, updated.Result!.RecordedAt);
        Assert.True(updated.Result.IsEdited);
    }

    [Fact]
    public async Task 進行中のセッションの成果は訂正できない()
    {
        var session = await StartAsync();

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.UpdateResultAsync(session.Id, 0, Result(), Ct));
    }

    [Fact]
    public async Task 中断終了したセッションの成果は訂正できない()
    {
        var session = await StartAsync();
        _clock.UtcNow = StartedAt.AddMinutes(8);
        await _service.AbandonAsync(session.Id, null, Ct);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.UpdateResultAsync(session.Id, 0, Result(), Ct));
    }

    // ------------------------------------------------------------------
    // 履歴
    // ------------------------------------------------------------------

    private async Task CompleteSessionAsync(DateTimeOffset startedAt, int minutes, long? taskId = null)
    {
        _clock.UtcNow = startedAt;
        var session = await _service.StartAsync(
            taskId ?? _taskId, _workTypeId, PreWorkState(), WorkContext(), Ct);

        _clock.UtcNow = startedAt.AddMinutes(minutes);
        await _service.FinishAsync(session.Id, 0, Result(), Ct);
    }

    [Fact]
    public async Task 履歴が日付単位でまとまる()
    {
        // JST 2026-08-04 09:12 と 13:00、JST 2026-08-03 10:00
        await CompleteSessionAsync(new DateTimeOffset(2026, 8, 4, 0, 12, 0, TimeSpan.Zero), 93);
        await CompleteSessionAsync(new DateTimeOffset(2026, 8, 4, 4, 0, 0, TimeSpan.Zero), 90);
        await CompleteSessionAsync(new DateTimeOffset(2026, 8, 3, 1, 0, 0, TimeSpan.Zero), 60);

        var history = await _service.GetHistoryAsync(
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 4), null, null, Ct);

        Assert.Equal(2, history.Count);
        Assert.Equal(new DateOnly(2026, 8, 4), history[0].Date);
        Assert.Equal(2, history[0].Sessions.Count);
        Assert.Equal(183, history[0].TotalMinutes);
        Assert.Equal(2, history[0].CompletedCount);
        Assert.Equal(new DateOnly(2026, 8, 3), history[1].Date);
    }

    [Fact]
    public async Task 履歴の日付とセッションは新しい順になる()
    {
        await CompleteSessionAsync(new DateTimeOffset(2026, 8, 4, 0, 12, 0, TimeSpan.Zero), 93);
        await CompleteSessionAsync(new DateTimeOffset(2026, 8, 4, 4, 0, 0, TimeSpan.Zero), 90);

        var history = await _service.GetHistoryAsync(
            new DateOnly(2026, 8, 4), new DateOnly(2026, 8, 4), null, null, Ct);

        var sessions = history[0].Sessions;
        Assert.True(sessions[0].StartedAt > sessions[1].StartedAt);
    }

    /// <summary>
    /// docs/08-technical-design.md §6.3 T-20。
    /// JST 22:00 開始・翌 01:00 終了のセッションは開始日側にまとまる。
    /// </summary>
    [Fact]
    public async Task 深夜をまたぐセッションは開始日にまとまる()
    {
        // UTC 13:00 = JST 22:00、終了は JST 翌 01:00
        await CompleteSessionAsync(new DateTimeOffset(2026, 8, 4, 13, 0, 0, TimeSpan.Zero), 180);

        var history = await _service.GetHistoryAsync(
            new DateOnly(2026, 8, 4), new DateOnly(2026, 8, 5), null, null, Ct);

        Assert.Single(history);
        Assert.Equal(new DateOnly(2026, 8, 4), history[0].Date);
    }

    [Fact]
    public async Task 履歴をタスクで絞り込める()
    {
        var otherTaskId = _taskItems.Seed("別の作業", _workTypeId).Id;
        await CompleteSessionAsync(new DateTimeOffset(2026, 8, 4, 0, 12, 0, TimeSpan.Zero), 93);
        await CompleteSessionAsync(new DateTimeOffset(2026, 8, 4, 4, 0, 0, TimeSpan.Zero), 90, otherTaskId);

        var history = await _service.GetHistoryAsync(
            new DateOnly(2026, 8, 4), new DateOnly(2026, 8, 4), null, otherTaskId, Ct);

        Assert.Single(history);
        Assert.Single(history[0].Sessions);
        Assert.Equal(otherTaskId, history[0].Sessions[0].TaskItemId);
    }

    [Fact]
    public async Task 履歴を状態で絞り込める()
    {
        await CompleteSessionAsync(new DateTimeOffset(2026, 8, 4, 0, 12, 0, TimeSpan.Zero), 93);

        _clock.UtcNow = new DateTimeOffset(2026, 8, 4, 4, 0, 0, TimeSpan.Zero);
        var abandoned = await StartAsync();
        _clock.UtcNow = _clock.UtcNow.AddMinutes(8);
        await _service.AbandonAsync(abandoned.Id, null, Ct);

        var history = await _service.GetHistoryAsync(
            new DateOnly(2026, 8, 4), new DateOnly(2026, 8, 4), SessionStatus.Abandoned, null, Ct);

        Assert.Single(history[0].Sessions);
        Assert.Equal(SessionStatus.Abandoned, history[0].Sessions[0].Status);
        Assert.Equal(1, history[0].AbandonedCount);
    }

    [Fact]
    public async Task 記録が無い期間は空になる()
    {
        var history = await _service.GetHistoryAsync(
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), null, null, Ct);

        Assert.Empty(history);
    }

    [Fact]
    public async Task 開始日が終了日より後なら例外になる()
    {
        await Assert.ThrowsAsync<DomainRuleException>(
            () => _service.GetHistoryAsync(
                new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 4), null, null, Ct));
    }
}
