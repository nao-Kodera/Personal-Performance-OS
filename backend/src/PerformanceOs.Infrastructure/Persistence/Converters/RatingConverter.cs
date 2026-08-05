using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PerformanceOs.Domain.ValueObjects;

namespace PerformanceOs.Infrastructure.Persistence.Converters;

/// <summary>
/// <see cref="Rating"/> と smallint の相互変換。
/// </summary>
/// <remarks>
/// 読み込み時も <see cref="Rating"/> のコンストラクタを通すため、DB に
/// 範囲外の値があればその時点で例外になる。DB の CHECK 制約と合わせて
/// 二重の担保になる（docs/06-database-design.md §2）。
/// </remarks>
public sealed class RatingConverter : ValueConverter<Rating, short>
{
    public RatingConverter()
        : base(rating => (short)rating.Value, value => new Rating(value))
    {
    }
}
