using PerformanceOs.Domain.Common;
using PerformanceOs.Domain.ValueObjects;

namespace PerformanceOs.Domain.Tests.ValueObjects;

public class RatingTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void 一から五の値で生成できる(int value)
    {
        var rating = new Rating(value);

        Assert.Equal(value, rating.Value);
    }

    /// <summary>
    /// docs/08-technical-design.md §6.2 T-11。
    /// 範囲外の値が DB に到達する前に、生成時点で弾く。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void 範囲外の値は例外になる(int value)
    {
        Assert.Throws<DomainException>(() => new Rating(value));
    }

    [Fact]
    public void 同じ値は等価である()
    {
        Assert.Equal(new Rating(3), new Rating(3));
    }

    [Fact]
    public void 異なる値は等価でない()
    {
        Assert.NotEqual(new Rating(3), new Rating(4));
    }

    [Fact]
    public void 文字列表現は数値のみ()
    {
        Assert.Equal("4", new Rating(4).ToString());
    }
}
