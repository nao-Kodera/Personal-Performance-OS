using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Application.WorkSessions;

/// <summary>
/// 履歴の 1 日分（docs/07-api-design.md §2.18）。
/// </summary>
/// <remarks>
/// <para>
/// グループのキーは各セッションの<b>開始時刻を JST に変換した日付</b>である。
/// 22:00 開始・翌 01:00 終了のセッションは開始日側に入る
/// （docs/02-glossary.md §4）。
/// </para>
/// <para>
/// API 設計 §2.18 はこの単位に日次コンディションを併記するが、
/// DailyCondition は T-20 の概念のため、この型にはまだ含めない。
/// </para>
/// </remarks>
/// <param name="TotalMinutes">
/// 完了・中断終了を問わず、終了済みセッションの実作業時間の合計。
/// </param>
public sealed record WorkSessionDay(
    DateOnly Date,
    IReadOnlyList<WorkSession> Sessions,
    int CompletedCount,
    int AbandonedCount,
    int TotalMinutes);

/// <summary>
/// 終了処理の結果。警告を伴う（docs/07-api-design.md §2.15）。
/// </summary>
public sealed record FinishedWorkSession(
    WorkSession Session,
    IReadOnlyList<SessionWarning> Warnings);
