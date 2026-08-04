namespace PerformanceOs.Domain.Common;

/// <summary>
/// 識別子を持つエンティティの基底。
/// </summary>
/// <remarks>
/// <para>
/// Id は DB の IDENTITY により採番される（docs/06-database-design.md §0）。
/// 永続化されるまでは 0 である。
/// </para>
/// <para>
/// 等価性比較は実装しない。ドメイン層でエンティティ同士を比較する用途が
/// 現時点で存在せず、EF Core は主キーで追跡するため
/// （docs/08-technical-design.md §0「必要になるまで入れない」）。
/// </para>
/// </remarks>
public abstract class Entity
{
    public long Id { get; protected set; }
}
