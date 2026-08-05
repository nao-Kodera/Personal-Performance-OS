using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PerformanceOs.Domain.ValueObjects;

namespace PerformanceOs.Infrastructure.Persistence.Converters;

/// <summary>
/// <see cref="SleepDuration"/> と integer（分）の相互変換。
/// </summary>
/// <remarks>
/// 読み込み時も <see cref="SleepDuration"/> のコンストラクタを通すため、DB に
/// 15 分単位でない値や範囲外の値があればその時点で例外になる。DB の CHECK 制約
/// と合わせて二重の担保になる（docs/06-database-design.md §2.3）。
/// </remarks>
public sealed class SleepDurationConverter : ValueConverter<SleepDuration, int>
{
    public SleepDurationConverter()
        : base(sleep => sleep.Minutes, minutes => new SleepDuration(minutes))
    {
    }
}
