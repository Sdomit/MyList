using System.Globalization;
using MahApps.Metro.IconPacks;
using MyList.Helpers;
using Xunit;

namespace MyList.Tests.Helpers;

public class TrendDeltaToLucideKindConverterTests
{
    private readonly TrendDeltaToLucideKindConverter _sut = new();

    [Fact]
    public void Convert_PositiveDelta_ReturnsTrendingUp()
    {
        var result = _sut.Convert(5, typeof(PackIconLucideKind), null!, CultureInfo.InvariantCulture);
        Assert.Equal(PackIconLucideKind.TrendingUp, result);
    }

    [Fact]
    public void Convert_NegativeDelta_ReturnsTrendingDown()
    {
        var result = _sut.Convert(-3, typeof(PackIconLucideKind), null!, CultureInfo.InvariantCulture);
        Assert.Equal(PackIconLucideKind.TrendingDown, result);
    }

    [Fact]
    public void Convert_ZeroDelta_ReturnsMinus()
    {
        var result = _sut.Convert(0, typeof(PackIconLucideKind), null!, CultureInfo.InvariantCulture);
        Assert.Equal(PackIconLucideKind.Minus, result);
    }

    [Fact]
    public void Convert_NonIntValue_TreatsAsZero()
    {
        var result = _sut.Convert("foo", typeof(PackIconLucideKind), null!, CultureInfo.InvariantCulture);
        Assert.Equal(PackIconLucideKind.Minus, result);
    }
}
