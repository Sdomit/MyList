using System.Globalization;
using System.Windows;
using MyList.Helpers;
using MyList.Models;
using Xunit;

namespace MyList.Tests.Helpers;

public class KindToBadgeLetterConverterTests
{
    private readonly KindToBadgeLetterConverter _sut = new();

    [Theory]
    [InlineData(ContentKind.File, "F")]
    [InlineData(ContentKind.Folder, "D")]
    [InlineData(ContentKind.Mtab, "M")]
    [InlineData(ContentKind.Clip, "C")]
    [InlineData(ContentKind.Action, "A")]
    public void Convert_MapsKindToLetter(ContentKind kind, string expected)
    {
        var result = _sut.Convert(kind, typeof(string), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_NullValue_ReturnsUnsetValue()
    {
        var result = _sut.Convert(null!, typeof(string), null!, CultureInfo.InvariantCulture);
        Assert.Equal(DependencyProperty.UnsetValue, result);
    }

    [Fact]
    public void Convert_WrongType_ReturnsUnsetValue()
    {
        var result = _sut.Convert("nonsense", typeof(string), null!, CultureInfo.InvariantCulture);
        Assert.Equal(DependencyProperty.UnsetValue, result);
    }
}
