using PerformanceOs.Api.Contracts.Responses;
using PerformanceOs.Application.WorkSessions;
using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Api.Mapping;

/// <summary>作業セッションのレスポンス変換。</summary>
public static class WorkSessionMappings
{
    public static WorkSessionResponse ToResponse(
        this WorkSession session,
        WorkSessionLabels labels,
        IReadOnlyList<SessionWarning>? warnings = null)
        => new(
            session.Id,
            session.TaskItemId,
            labels.TaskTitle(session.TaskItemId),
            session.WorkTypeId,
            labels.WorkTypeName(session.WorkTypeId),
            session.PlannedWorkId,
            session.StartedAt,
            session.FinishedAt,
            session.Status,
            session.DurationMinutes,
            session.InterruptionCount,
            session.AbandonNote,
            session.TimeBand,
            new PreWorkStateResponse(
                session.PreWorkState.FatigueLevel.Value,
                session.PreWorkState.ExpectedFocusLevel.Value,
                session.PreWorkState.MoodLevel.Value,
                session.PreWorkState.RecordedAt),
            new WorkContextResponse(
                session.WorkContext.WorkLocation,
                session.WorkContext.LocationNote,
                session.WorkContext.MeetingCount,
                session.WorkContext.InterruptionExpected,
                session.WorkContext.RecordedAt),
            session.Result is null ? null : new PerformanceResultResponse(
                session.Result.FocusLevel.Value,
                session.Result.OutputLevel.Value,
                session.Result.AccuracyLevel.Value,
                session.Result.SatisfactionLevel.Value,
                session.Result.FatigueAfter.Value,
                session.Result.Note,
                session.Result.RecordedAt,
                session.Result.UpdatedAt,
                session.Result.IsEdited),
            session.FatigueDelta,
            session.FocusGap,
            warnings ?? []);

    public static WorkSessionDayResponse ToResponse(
        this WorkSessionDay day, WorkSessionLabels labels)
        => new(
            day.Date,
            day.Date.DayOfWeek,
            day.Sessions.Select(x => x.ToResponse(labels)).ToList(),
            new WorkSessionDaySummaryResponse(
                day.CompletedCount, day.AbandonedCount, day.TotalMinutes));

    public static IReadOnlyList<WorkSessionDayResponse> ToResponses(
        this IReadOnlyList<WorkSessionDay> days, WorkSessionLabels labels)
        => days.Select(x => x.ToResponse(labels)).ToList();
}
