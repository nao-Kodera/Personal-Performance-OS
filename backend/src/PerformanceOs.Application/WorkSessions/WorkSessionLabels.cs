namespace PerformanceOs.Application.WorkSessions;

/// <summary>
/// セッション表示用の名称解決表。
/// </summary>
/// <remarks>
/// <para>
/// WorkSession は TaskItem / WorkType を ID でしか参照しない（集約間は ID 参照）。
/// 一方 API はタスク名と作業タイプ名を返す（docs/07-api-design.md §2.13 / §2.18）。
/// </para>
/// <para>
/// セッションごとに引くと N+1 になるため、まとめて 1 回だけ引く。
/// 問い合わせ回数はセッション件数に依存しない。
/// </para>
/// <para>
/// アーカイブ済みタスク・無効な作業タイプも含める。過去のセッションは
/// それらを参照し続けるため、除外すると履歴の名称が欠ける。
/// </para>
/// </remarks>
public sealed record WorkSessionLabels(
    IReadOnlyDictionary<long, string> TaskTitles,
    IReadOnlyDictionary<long, string> WorkTypeNames)
{
    /// <summary>解決できない場合は空文字ではなく既定の表記を返す。</summary>
    public string TaskTitle(long taskItemId)
        => TaskTitles.TryGetValue(taskItemId, out var title) ? title : "(不明なタスク)";

    public string WorkTypeName(long workTypeId)
        => WorkTypeNames.TryGetValue(workTypeId, out var name) ? name : "(不明な作業タイプ)";
}
