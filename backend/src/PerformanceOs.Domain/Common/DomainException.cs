namespace PerformanceOs.Domain.Common;

/// <summary>
/// ドメインの不変条件に違反したことを表す例外。
/// API 層で 422 (domain-rule) に変換される（docs/07-api-design.md §0.2）。
/// </summary>
/// <remarks>
/// ドメイン層の例外はこの 1 種類のみとする。新しい例外階層を作らないこと。
/// アプリケーション層の例外（NotFound / Conflict / DomainRule）は
/// PerformanceOs.Application.Common に置く。
/// </remarks>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
