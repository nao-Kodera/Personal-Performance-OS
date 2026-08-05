namespace PerformanceOs.Api.Middleware;

/// <summary>
/// ProblemDetails の type に使う URI（docs/07-api-design.md §0.2）。
/// </summary>
public static class ProblemTypes
{
    private const string Prefix = "https://performance-os.local/errors/";

    /// <summary>400: リクエスト形式・値の範囲エラー。</summary>
    public const string Validation = Prefix + "validation";

    /// <summary>404: 指定 ID のリソースが存在しない。</summary>
    public const string NotFound = Prefix + "not-found";

    /// <summary>409: 現在の状態と操作が矛盾する。</summary>
    public const string Conflict = Prefix + "conflict";

    /// <summary>422: 値そのものがドメイン規則に反する。</summary>
    public const string DomainRule = Prefix + "domain-rule";

    /// <summary>500: 想定外のエラー。</summary>
    public const string Internal = Prefix + "internal";
}
