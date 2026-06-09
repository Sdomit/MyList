using System.Globalization;
using FluentAssertions;
using MyList.Helpers;
using MyList.Models;
using Xunit;

namespace MyList.Tests.Helpers;

public class TrajectoryBarTooltipConverterTests
{
    private readonly TrajectoryBarTooltipConverter _sut = new();

    private static TrajectoryBar Bar(int dayOffset, int accessCount)
        => new(10.0, 0.5) { DayOffset = dayOffset, AccessCount = accessCount };

    [Fact]
    public void Convert_TodayWithOpens_FormatsToday()
    {
        var result = _sut.Convert(Bar(0, 3), typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be("Today — 3 opens");
    }

    [Fact]
    public void Convert_YesterdaySingleOpen_FormatsSingular()
    {
        var result = _sut.Convert(Bar(1, 1), typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be("Yesterday — 1 open");
    }

    [Fact]
    public void Convert_DaysAgoNoOpens_FormatsNoOpens()
    {
        var result = _sut.Convert(Bar(5, 0), typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be("5 days ago — no opens");
    }

    [Fact]
    public void Convert_TodayZeroOpens_FormatsTodayNoOpens()
    {
        var result = _sut.Convert(Bar(0, 0), typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be("Today — no opens");
    }

    [Fact]
    public void Convert_NonBarValue_ReturnsBindingDoNothing()
    {
        var result = _sut.Convert("foo", typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be(System.Windows.Data.Binding.DoNothing);
    }
}
