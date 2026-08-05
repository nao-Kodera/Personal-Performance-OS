using PerformanceOs.Application.WorkSessions;
using PerformanceOs.Domain.Time;
using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Api.Contracts.Responses;

/// <summary>
/// 作業前状態。<b>更新できない</b>（PS-2）。更新用のエンドポイントも存在しない。
/// </summary>
public sealed record PreWorkStateResponse(
    int FatigueLevel,
    int ExpectedFocusLevel,
    int MoodLevel,
    DateTimeOffset RecordedAt);

/// <summary>作業環境。<b>更新できない</b>（WC-3）。</summary>
public sealed record WorkContextResponse(
    WorkLocation WorkLocation,
    string? LocationNote,
    int MeetingCount,
    bool InterruptionExpected,
    DateTimeOffset RecordedAt);

/// <summary>
/// 成果評価。
/// </summary>
/// <param name="IsEdited">
/// 事後に訂正されたか（RecordedAt と UpdatedAt が異なるか）。
/// 分析の信頼性を評価する材料になる。
/// </param>
public sealed record PerformanceResultResponse(
    int FocusLevel,
    int OutputLevel,
    int AccuracyLevel,
    int SatisfactionLevel,
    int FatigueAfter,
    string? Note,
    DateTimeOffset RecordedAt,
    DateTimeOffset UpdatedAt,
    bool IsEdited);

/// <summary>
/// 作業セッション（docs/07-api-design.md §2.13 / §2.15 / §2.16 / §2.18）。
/// </summary>
/// <remarks>
/// <para>
/// 日時はすべて UTC。JST への変換はクライアントで行う
/// （docs/07-api-design.md §0.1）。
/// </para>
/// <para>
/// <see cref="DurationMinutes"/> / <see cref="FatigueDelta"/> /
/// <see cref="FocusGap"/> / <see cref="TimeBand"/> は保存されない導出値である
/// （docs/04-analytics-spec.md §5）。
/// </para>
/// </remarks>
/// <param name="Warnings">
/// 終了時のみ内容を持つ。他の操作では空。保存を妨げない情報である。
/// </param>
public sealed record WorkSessionResponse(
    long Id,
    long TaskItemId,
    string TaskTitle,
    long WorkTypeId,
    string WorkTypeName,
    long? PlannedWorkId,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    SessionStatus Status,
    int? DurationMinutes,
    int InterruptionCount,
    string? AbandonNote,
    TimeBand TimeBand,
    PreWorkStateResponse PreWorkState,
    WorkContextResponse WorkContext,
    PerformanceResultResponse? Result,
    int? FatigueDelta,
    int? FocusGap,
    IReadOnlyList<SessionWarning> Warnings);

/// <summary>履歴の 1 日分の集計。</summary>
public sealed record WorkSessionDaySummaryResponse(
    int CompletedCount,
    int AbandonedCount,
    int TotalMinutes);

/// <summary>
/// 履歴の 1 日分（docs/07-api-design.md §2.18）。
/// </summary>
/// <remarks>
/// 日付は各セッションの開始時刻を JST に変換した値。深夜をまたぐセッションは
/// 開始日側に入る（docs/02-glossary.md §4）。
/// <para>
/// API 設計 §2.18 はここに日次コンディションを併記するが、DailyCondition は
/// T-20 の概念のため、まだ含めない。
/// </para>
/// </remarks>
public sealed record WorkSessionDayResponse(
    DateOnly Date,
    DayOfWeek DayOfWeek,
    IReadOnlyList<WorkSessionResponse> Sessions,
    WorkSessionDaySummaryResponse Summary);
