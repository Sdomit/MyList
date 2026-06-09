using System.Globalization;
using System.Windows;
using FluentAssertions;
using MyList.Helpers;
using Xunit;

namespace MyList.Tests.Helpers;

public class IntPlusOneConverterTests
{
    private readonly IntPlusOneConverter _sut = new();

    [Theory]
    [InlineData(0, "1")]
    [InlineData(1, "2")]
    [InlineData(8, "9")]
    public void Convert_LabelMode_ReturnsOneBasedIndexAsString(int alternationIndex, string expected)
    {
        var result = _sut.Convert(alternationIndex, typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(99)]
    public void Convert_LabelMode_TenOrMoreReturnsEmpty(int alternationIndex)
    {
        var result = _sut.Convert(alternationIndex, typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be(string.Empty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(8)]
    public void Convert_VisibilityMode_OneToNineReturnsVisible(int alternationIndex)
    {
        var result = _sut.Convert(alternationIndex, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        result.Should().Be(Visibility.Visible);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(50)]
    public void Convert_VisibilityMode_TenOrMoreReturnsCollapsed(int alternationIndex)
    {
        var result = _sut.Convert(alternationIndex, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        result.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void Convert_NegativeIndex_VisibilityMode_ReturnsCollapsed()
    {
        var result = _sut.Convert(-1, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        result.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void Convert_NegativeIndex_LabelMode_ReturnsEmpty()
    {
        var result = _sut.Convert(-1, typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void Convert_NonIntValue_LabelMode_ReturnsEmpty()
    {
        var result = _sut.Convert("foo", typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be(string.Empty);
    }
}
