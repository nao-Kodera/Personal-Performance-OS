using System.Globalization;
using PerformanceOs.Domain.Common;

namespace PerformanceOs.Domain.ValueObjects;

/// <summary>
/// 1〜5 の主観評価値。すべての評価指標がこの型を使う（docs/02-glossary.md §2）。
/// </summary>
/// <remarks>
/// <para>
/// 評価尺度は全期間で固定する。段階数を変更すると過去データとの比較が
/// 成立しなくなる（docs/01-product-requirements.md §5.1）。
/// </para>
/// <para>
/// 尺度の向きは指標によって異なる。疲労度・ストレスは「高いほど悪い」、
/// それ以外は「高いほど良い」（docs/02-glossary.md §2.2）。
/// この型は向きを持たない。表示時に反転しないこと。
/// </para>
/// <para>
/// 構造体のため <c>default(Rating)</c> はコンストラクタを通らず Value が 0 になる。
/// エンティティは Rating を <c>private set</c> でしか公開せず、生成は必ず
/// コンストラクタ経由であるため通常の経路では発生しない。加えて DB の
/// CHECK 制約が最終的な担保になる（docs/06-database-design.md §2）。
/// </para>
/// </remarks>
public readonly record struct Rating
{
    public const int Min = 1;
    public const int Max = 5;

    public int Value { get; }

    public Rating(int value)
    {
        if (value is < Min or > Max)
        {
            throw new DomainException(
                $"評価値は {Min}〜{Max} の範囲である必要があります: {value}");
        }

        Value = value;
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
