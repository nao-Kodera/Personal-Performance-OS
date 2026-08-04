namespace PerformanceOs.Domain.Time;

/// <summary>
/// JST（Asia/Tokyo）基準の日付・時間帯を導出する。
/// </summary>
/// <remarks>
/// <para>
/// <b>JST 変換ロジックはこのクラスに閉じ込める。</b>
/// 各所で個別に <see cref="TimeZoneInfo"/> 変換を書かないこと
/// （docs/05-domain-design.md §5.3、docs/08-technical-design.md §3.1）。
/// </para>
/// <para>
/// 日付境界は JST の 00:00 とする。深夜作業は開始時刻の属する日に集計される。
/// 22:00 開始・翌 01:00 終了のセッションは開始日側に入り、終了日側には含めない
/// （docs/02-glossary.md §4）。
/// </para>
/// <para>
/// <b>同じ変換が SQL 側（v_completed_sessions ビュー）にも存在する。</b>
/// 片方だけ変更されると API の表示と分析結果がずれるため、T-25 で 24 時間分の
/// 一致を確認すること（docs/08-technical-design.md §6.3）。
/// </para>
/// </remarks>
public static class JstCalendar
{
    /// <remarks>
    /// .NET 6 以降は Windows 上でも IANA ID が解決されるため、
    /// Linux コンテナ・Windows 開発機の双方で "Asia/Tokyo" を使える
    /// （docs/08-technical-design.md §3.1）。
    /// </remarks>
    private static readonly TimeZoneInfo Jst = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

    /// <summary>
    /// 指定時刻が属する JST 基準の日付を返す。
    /// WorkSession の所属日、DailyCondition の対象日はすべてこの基準による。
    /// </summary>
    public static DateOnly ToJstDate(DateTimeOffset instant)
        => DateOnly.FromDateTime(ToJst(instant).DateTime);

    /// <summary>
    /// 指定時刻が属する時間帯区分を返す（docs/02-glossary.md §3.2）。
    /// </summary>
    public static TimeBand ToTimeBand(DateTimeOffset instant)
    {
        // Evening は 17:00–04:59 と日をまたぐ。他の 3 区分に該当しないものが Evening になる。
        return ToJst(instant).Hour switch
        {
            >= 5 and <= 8 => TimeBand.EarlyMorning,
            >= 9 and <= 11 => TimeBand.Morning,
            >= 12 and <= 16 => TimeBand.Afternoon,
            _ => TimeBand.Evening,
        };
    }

    private static DateTimeOffset ToJst(DateTimeOffset instant)
        => TimeZoneInfo.ConvertTime(instant, Jst);
}
