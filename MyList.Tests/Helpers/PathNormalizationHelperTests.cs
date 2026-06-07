using System.Linq;
using MyList.Helpers;
using Xunit;

namespace MyList.Tests.Helpers;

public class PathNormalizationHelperTests
{
    [Fact]
    public void CleanCandidate_NullOrWhitespace_ReturnsNull()
    {
        Assert.Null(PathNormalizationHelper.CleanCandidate(null));
        Assert.Null(PathNormalizationHelper.CleanCandidate(string.Empty));
        Assert.Null(PathNormalizationHelper.CleanCandidate("   "));
    }

    [Fact]
    public void CleanCandidate_StripsSurroundingQuotes()
    {
        Assert.Equal(@"C:\foo\bar", PathNormalizationHelper.CleanCandidate("\"C:\\foo\\bar\""));
    }

    [Fact]
    public void CleanCandidate_NormalizesForwardSlashes()
    {
        Assert.Equal(@"C:\foo\bar", PathNormalizationHelper.CleanCandidate("C:/foo/bar"));
    }

    [Fact]
    public void CleanCandidate_TrimsWhitespace()
    {
        Assert.Equal(@"C:\foo", PathNormalizationHelper.CleanCandidate("   C:\\foo   "));
    }

    [Theory]
    [InlineData(@"C:\foo\bar", true)]
    [InlineData(@"D:\", true)]
    [InlineData(@"\\server\share\folder", true)]
    [InlineData(@"\\server\share", true)]
    [InlineData(@"foo\bar", false)]
    [InlineData(@"", false)]
    [InlineData(null, false)]
    [InlineData(@"\\server", false)]
    public void IsValidPathCandidate_ChecksRootingAndUncShape(string? input, bool expected)
    {
        Assert.Equal(expected, PathNormalizationHelper.IsValidPathCandidate(input));
    }

    [Fact]
    public void TryNormalizeUserPath_EmptyInput_ReturnsEmptyStatus()
    {
        var ok = PathNormalizationHelper.TryNormalizeUserPath(null, out var normalized, out var key, out var status);
        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
        Assert.Equal(string.Empty, key);
        Assert.Equal(PathValidationStatus.Empty, status);
    }

    [Fact]
    public void TryNormalizeUserPath_RelativePath_ReturnsNotRooted()
    {
        var ok = PathNormalizationHelper.TryNormalizeUserPath("foo\\bar", out _, out _, out var status);
        Assert.False(ok);
        Assert.Equal(PathValidationStatus.NotRooted, status);
    }

    [Fact]
    public void TryNormalizeUserPath_IncompleteUnc_ReturnsInvalidFormat()
    {
        var ok = PathNormalizationHelper.TryNormalizeUserPath(@"\\server", out _, out _, out var status);
        Assert.False(ok);
        Assert.Equal(PathValidationStatus.InvalidFormat, status);
    }

    [Fact]
    public void TryNormalizeUserPath_DriveRoot_FlipsSeparatorsAndBuildsUppercaseKey()
    {
        var ok = PathNormalizationHelper.TryNormalizeUserPath("C:/foo/bar/", out var normalized, out var key, out var status);
        Assert.True(ok);
        Assert.Equal(PathValidationStatus.Valid, status);
        Assert.Equal(@"C:\foo\bar", normalized);
        Assert.Equal(@"C:\FOO\BAR", key);
    }

    [Fact]
    public void TryNormalizeUserPath_DriveRootOnly_PreservesTrailingBackslash()
    {
        var ok = PathNormalizationHelper.TryNormalizeUserPath("D:\\", out var normalized, out var key, out var status);
        Assert.True(ok);
        Assert.Equal(PathValidationStatus.Valid, status);
        Assert.Equal(@"D:\", normalized);
        Assert.Equal(@"D:\", key);
    }

    [Fact]
    public void TryNormalizeUserPath_UncPath_PreservesUncRoot()
    {
        var ok = PathNormalizationHelper.TryNormalizeUserPath(@"\\server\share\folder\", out var normalized, out var key, out var status);
        Assert.True(ok);
        Assert.Equal(PathValidationStatus.Valid, status);
        Assert.Equal(@"\\server\share\folder", normalized);
        Assert.Equal(@"\\SERVER\SHARE\FOLDER", key);
    }

    [Fact]
    public void GetPathKey_InvalidPath_ReturnsNull()
    {
        Assert.Null(PathNormalizationHelper.GetPathKey(null));
        Assert.Null(PathNormalizationHelper.GetPathKey("foo\\bar"));
    }

    [Fact]
    public void GetPathKey_ValidPath_ReturnsUppercaseKey()
    {
        Assert.Equal(@"C:\WIN\NOTEPAD.EXE", PathNormalizationHelper.GetPathKey(@"c:\Win\Notepad.exe"));
    }

    [Fact]
    public void ExtractClipboardCandidates_EmptyOrWhitespace_ReturnsNoCandidates()
    {
        Assert.Empty(PathNormalizationHelper.ExtractClipboardCandidates(null));
        Assert.Empty(PathNormalizationHelper.ExtractClipboardCandidates(string.Empty));
        Assert.Empty(PathNormalizationHelper.ExtractClipboardCandidates("   \n   "));
    }

    [Fact]
    public void ExtractClipboardCandidates_RawLine_YieldsLineAsCandidate()
    {
        var candidates = PathNormalizationHelper.ExtractClipboardCandidates(@"C:\foo\bar").ToList();
        Assert.Contains(@"C:\foo\bar", candidates);
    }

    [Fact]
    public void ExtractClipboardCandidates_QuotedAndInline_DedupesAcrossPasses()
    {
        var input = "Open \"C:\\foo\\bar\" and also C:\\foo\\bar again";
        var candidates = PathNormalizationHelper.ExtractClipboardCandidates(input).ToList();
        Assert.Contains(@"C:\foo\bar", candidates);
        Assert.Single(candidates, c => c == @"C:\foo\bar");
    }

    [Fact]
    public void ExtractClipboardCandidates_PowerShellPrompt_ExtractsCurrentDirectory()
    {
        var candidates = PathNormalizationHelper.ExtractClipboardCandidates(@"PS C:\Users\me\projects> git status").ToList();
        Assert.Contains(@"C:\Users\me\projects", candidates);
    }

    [Fact]
    public void ExtractClipboardCandidates_CmdPrompt_ExtractsCurrentDirectory()
    {
        var candidates = PathNormalizationHelper.ExtractClipboardCandidates(@"C:\Users\me>dir").ToList();
        Assert.Contains(@"C:\Users\me", candidates);
    }

    [Fact]
    public void ExtractClipboardCandidates_UncInlineMatch_PicksUpUncPath()
    {
        var candidates = PathNormalizationHelper.ExtractClipboardCandidates(@"open \\server\share\folder later").ToList();
        Assert.Contains(@"\\server\share\folder", candidates);
    }
}
