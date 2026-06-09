using System;
using System.Globalization;
using System.Windows.Media;
using FluentAssertions;
using MyList.Helpers;
using Xunit;

namespace MyList.Tests.Helpers;

public class PercentToDashArrayConverterTests
{
    private const double Circumference = Math.PI * 22.0;
    private readonly PercentToDashArrayConverter _sut = new();

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    public void Convert_DoubleScore_ReturnsFilledAndEmptyDashes(double score)
    {
        var result = _sut.Convert(score, typeof(DoubleCollection), null!, CultureInfo.InvariantCulture);
        result.Should().BeOfType<DoubleCollection>();
        var dashes = (DoubleCollection)result;
        dashes.Should().HaveCount(2);
        dashes[0].Should().BeApproximately(score * Circumference, 0.001);
        dashes[1].Should().BeApproximately(Circumference - score * Circumference, 0.001);
    }

    [Fact]
    public void Convert_NegativeClampsToZero()
    {
        var result = (DoubleCollection)_sut.Convert(-0.5, typeof(DoubleCollection), null!, CultureInfo.InvariantCulture);
        result[0].Should().Be(0.0);
        result[1].Should().BeApproximately(Circumference, 0.001);
    }

    [Fact]
    public void Convert_OverOneClampsToOne()
    {
        var result = (DoubleCollection)_sut.Convert(1.5, typeof(DoubleCollection), null!, CultureInfo.InvariantCulture);
        result[0].Should().BeApproximately(Circumference, 0.001);
        result[1].Should().Be(0.0);
    }

    [Fact]
    public void Convert_NonNumericValue_TreatsAsZero()
    {
        var result = (DoubleCollection)_sut.Convert("foo", typeof(DoubleCollection), null!, CultureInfo.InvariantCulture);
        result[0].Should().Be(0.0);
    }
}
