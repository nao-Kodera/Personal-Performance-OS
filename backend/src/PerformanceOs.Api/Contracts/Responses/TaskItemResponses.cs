namespace PerformanceOs.Api.Contracts.Responses;

/// <summary>
/// タスク一覧の 1 行（docs/07-api-design.md §2.4）。
/// </summary>
/// <param name="LastUsedAt">
/// 最新セッションの開始時刻。一度も使われていなければ null。
/// 作業開始画面でタスクを直近使用順に出すために使う。
/// </param>
public sealed record TaskItemResponse(
    long Id,
    string Title,
    long DefaultWorkTypeId,
    string DefaultWorkTypeName,
    string? Note,
    bool IsArchived,
    DateTimeOffset? LastUsedAt,
    int SessionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 作成・更新・アーカイブの結果。
/// </summary>
/// <remarks>
/// <para>
/// 一覧と違い、集計値（lastUsedAt / sessionCount）を含まない。これらは
/// 射影クエリでしか得られず、更新のたびに追加の問い合わせが必要になるため。
/// </para>
/// <para>
/// 作業タイプ名も含まない。クライアントは選択肢として作業タイプ一覧を
/// 保持しており、ID から解決できる。
/// </para>
/// <para>
/// <b>完了状態を持たない。</b>アーカイブは完了ではなく選択肢からの除外である。
/// </para>
/// </remarks>
public sealed record TaskItemDetailResponse(
    long Id,
    string Title,
    long DefaultWorkTypeId,
    string? Note,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
