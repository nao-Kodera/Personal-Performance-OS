using System.Reflection;
using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Domain.Tests.WorkSessions;

/// <summary>
/// docs/08-technical-design.md §6.2 T-12。
/// </summary>
/// <remarks>
/// <para>
/// PreWorkState と WorkContext のイミュータブル性（PS-2 / WC-3）は、
/// 本プロダクトの分析が意味を持つための前提である。将来の変更で setter や
/// 更新メソッドが追加されると、結果を知った後に説明変数を書き換えられるようになり、
/// 分析結果が思い込みの写像になる。
/// </para>
/// <para>
/// EF Core のために public setter を付けた瞬間に失われるため、
/// テストで固定する（docs/08-technical-design.md §8 の禁止事項 1）。
/// </para>
/// </remarks>
public class ImmutabilityTests
{
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    public static TheoryData<Type> ImmutableTypes => new()
    {
        typeof(PreWorkState),
        typeof(WorkContext),
    };

    [Theory]
    [MemberData(nameof(ImmutableTypes))]
    public void 公開セッターを持たない(Type type)
    {
        var properties = type.GetProperties(Declared);

        // BindingFlags の誤りでこのテストが空振りしないことを保証する。
        Assert.NotEmpty(properties);

        var settable = properties
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(settable);
    }

    [Theory]
    [MemberData(nameof(ImmutableTypes))]
    public void 公開メソッドを持たない(Type type)
    {
        // プロパティのアクセサ以外に公開メソッドがあれば、状態を変える手段になりうる。
        var methods = type
            .GetMethods(Declared)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(methods);
    }

    [Theory]
    [MemberData(nameof(ImmutableTypes))]
    public void 公開コンストラクタを持たない(Type type)
    {
        // 集約ルートからのみ生成できること。
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));

        // 非公開コンストラクタは存在する（型を取り違えていないことの確認）。
        Assert.NotEmpty(type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
    }

    /// <summary>
    /// PR-4: 合成指標を持たない。5 指標を合成すると、どの要素が効いているか
    /// 分からなくなる（docs/02-glossary.md §6）。
    /// </summary>
    [Theory]
    [InlineData("Score")]
    [InlineData("Total")]
    [InlineData("Overall")]
    [InlineData("Average")]
    [InlineData("Composite")]
    public void 成果は合成指標を持たない(string forbidden)
    {
        var members = typeof(PerformanceResult).GetMembers().Select(m => m.Name).ToList();

        Assert.DoesNotContain(
            members,
            name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// WS-8: 開始・終了時刻を外部から設定できない。
    /// 時刻を手入力できると記録の事実性が失われ、実作業時間の分析が
    /// 記憶ベースの値に汚染される。
    /// </summary>
    [Theory]
    [InlineData(nameof(WorkSession.StartedAt))]
    [InlineData(nameof(WorkSession.FinishedAt))]
    public void セッションの時刻に公開セッターがない(string propertyName)
    {
        var property = typeof(WorkSession).GetProperty(propertyName);

        Assert.NotNull(property);
        Assert.False(property.SetMethod is { IsPublic: true });
    }
}
