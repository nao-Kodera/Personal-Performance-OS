using PerformanceOs.Domain.ValueObjects;
using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Domain.Tests.WorkSessions;

/// <summary>
/// テスト用の WorkSession 生成ヘルパー。
/// </summary>
internal static class WorkSessionFactory
{
    public const long TaskItemId = 12;
    public const long WorkTypeId = 2;

    /// <summary>UTC 2026-08-04 00:12 = JST 09:12（午前）。</summary>
    public static readonly DateTimeOffset StartedAt =
        new(2026, 8, 4, 0, 12, 0, TimeSpan.Zero);

    /// <summary>UTC 2026-08-04 01:45 = JST 10:45。開始から 93 分。</summary>
    public static readonly DateTimeOffset FinishedAt =
        new(2026, 8, 4, 1, 45, 0, TimeSpan.Zero);

    public static PreWorkStateInput PreWorkState(
        int fatigue = 2, int expectedFocus = 4, int mood = 4)
        => new(new Rating(fatigue), new Rating(expectedFocus), new Rating(mood));

    public static WorkContextInput WorkContext(
        WorkLocation location = WorkLocation.Home,
        string? locationNote = null,
        int meetingCount = 1,
        bool interruptionExpected = false)
        => new(location, locationNote, meetingCount, interruptionExpected);

    public static PerformanceResultInput Result(
        int focus = 4, int output = 4, int accuracy = 3,
        int satisfaction = 4, int fatigueAfter = 4, string? note = null)
        => new(
            new Rating(focus),
            new Rating(output),
            new Rating(accuracy),
            new Rating(satisfaction),
            new Rating(fatigueAfter),
            note);

    public static WorkSession Started(
        long? plannedWorkId = null,
        PreWorkStateInput? preWorkState = null,
        WorkContextInput? workContext = null,
        DateTimeOffset? now = null)
        => WorkSession.Start(
            TaskItemId,
            WorkTypeId,
            plannedWorkId,
            preWorkState ?? PreWorkState(),
            workContext ?? WorkContext(),
            now ?? StartedAt);

    public static WorkSession Completed(
        PerformanceResultInput? result = null,
        int interruptionCount = 1)
    {
        var session = Started();
        session.Finish(interruptionCount, result ?? Result(), FinishedAt);
        return session;
    }

    public static WorkSession Abandoned(string? note = "会議に呼ばれて中断")
    {
        var session = Started();
        session.Abandon(note, FinishedAt);
        return session;
    }
}
