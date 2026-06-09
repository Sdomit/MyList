using System.Globalization;
using System.Windows;
using MahApps.Metro.IconPacks;
using MyList.Helpers;
using MyList.Models;
using Xunit;

namespace MyList.Tests.Helpers;

public class KindToLucideKindConverterTests
{
    private readonly KindToLucideKindConverter _sut = new();

    [Theory]
    [InlineData(ContentKind.File, PackIconLucideKind.File)]
    [InlineData(ContentKind.Folder, PackIconLucideKind.Folder)]
    [InlineData(ContentKind.Mtab, PackIconLucideKind.Link)]
    [InlineData(ContentKind.Clip, PackIconLucideKind.ClipboardCheck)]
    [InlineData(ContentKind.Action, PackIconLucideKind.Zap)]
    public void Convert_MapsKindToLucide(ContentKind kind, PackIconLucideKind expected)
    {
        var result = _sut.Convert(kind, typeof(PackIconLucideKind), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_NullValue_ReturnsUnsetValue()
    {
        var result = _sut.Convert(null!, typeof(PackIconLucideKind), null!, CultureInfo.InvariantCulture);
        Assert.Equal(DependencyProperty.UnsetValue, result);
    }
}
