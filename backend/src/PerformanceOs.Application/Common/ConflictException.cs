namespace PerformanceOs.Application.Common;

/// <summary>
/// 現在の状態と操作が矛盾する。HTTP 409 に変換される。
/// </summary>
/// <remarks>
/// 時間をおけば成功しうる、または他の操作が先に必要な場合に使う。
/// 例: 進行中セッションが存在する状態での開始、終了済みセッションの再終了。
/// <para>
/// 値そのものがドメイン規則に反し、何度試しても成功しない場合は
/// <see cref="DomainRuleException"/>（422）を使う
/// （docs/07-api-design.md §0.2）。
/// </para>
/// </remarks>
public sealed class ConflictException : ApplicationException
{
    public ConflictException(string message) : base(message)
    {
    }
}
