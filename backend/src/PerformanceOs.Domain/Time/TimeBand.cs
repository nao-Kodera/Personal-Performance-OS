namespace PerformanceOs.Domain.Time;

/// <summary>
/// 時間帯区分。WorkSession の開始時刻（JST）から導出する（docs/02-glossary.md §3.2）。
/// </summary>
/// <remarks>
/// <para>
/// この値は保存しない。開始時刻から都度導出する。区分の定義を変更したとき、
/// 過去データにも新しい定義が適用される必要があるため（docs/05-domain-design.md §5.4）。
/// </para>
/// <para>
/// 宣言順が分析画面の表示順（時系列順）と一致している。
/// 平均値順に並べ替えないこと。時間帯は順序尺度であり、時系列に並べることで
/// 「午前がピークで午後に落ちる」という形が読み取れる（docs/04-analytics-spec.md A-03）。
/// </para>
/// </remarks>
public enum TimeBand
{
    /// <summary>早朝: 05:00–08:59 (JST)</summary>
    EarlyMorning,

    /// <summary>午前: 09:00–11:59 (JST)</summary>
    Morning,

    /// <summary>午後: 12:00–16:59 (JST)</summary>
    Afternoon,

    /// <summary>夜: 17:00–04:59 (JST)。日をまたぐ。</summary>
    Evening,
}
