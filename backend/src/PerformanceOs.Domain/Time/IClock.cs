namespace PerformanceOs.Domain.Time;

/// <summary>
/// 現在時刻の取得口。
/// </summary>
/// <remarks>
/// <para>
/// ドメイン層・アプリケーション層で <c>DateTime.Now</c> / <c>DateTime.UtcNow</c> を
/// 直接呼ばないこと。すべての時刻取得をこのインターフェース経由にする
/// （docs/08-technical-design.md §3.1）。
/// </para>
/// <para>
/// 理由は 2 つある。テストで時刻を固定できること（深夜をまたぐセッション・
/// 当日判定のテストに必須）と、JST 変換の実装が 1 箇所に集まることである。
/// </para>
/// </remarks>
public interface IClock
{
    /// <summary>現在の UTC 時刻。永続化される時刻は必ずこれを使う。</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// JST 基準の「今日」。DailyCondition / PlannedWork の当日判定に使う。
    /// </summary>
    /// <remarks>
    /// クライアントの時計を信用しないため、当日判定はサーバー側のこの値で行う
    /// （docs/07-api-design.md §2.9）。
    /// </remarks>
    DateOnly TodayJst { get; }
}
