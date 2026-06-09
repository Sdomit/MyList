using FluentAssertions;
using MyList.Helpers;
using MyList.Models;
using Xunit;

namespace MyList.Tests.Helpers;

public class ClipboardKindClassifierTests
{
    private sealed class FakeProbe : IFileSystemProbe
    {
        public bool FilesExist { get; init; }
        public bool DirsExist { get; init; }
        public bool FileExists(string path) => FilesExist;
        public bool DirectoryExists(string path) => DirsExist;
    }

    private static readonly FakeProbe NoFs = new();

    [Theory]
    [InlineData("shell:Downloads")]
    [InlineData("cmd:dir")]
    [InlineData("run:notepad")]
    [InlineData("SHELL:Documents")]
    public void Classify_ActionPrefix_ReturnsAction(string raw)
    {
        ClipboardKindClassifier.Classify(raw, null, NoFs).Should().Be(ContentKind.Action);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/path")]
    public void Classify_HttpUrl_ReturnsMtab(string raw)
    {
        ClipboardKindClassifier.Classify(raw, null, NoFs).Should().Be(ContentKind.Mtab);
    }

    [Fact]
    public void Classify_DirectoryExists_ReturnsFolder()
    {
        var probe = new FakeProbe { DirsExist = true };
        ClipboardKindClassifier.Classify("C:\\foo", "C:\\foo", probe).Should().Be(ContentKind.Folder);
    }

    [Fact]
    public void Classify_FileExists_ReturnsFile()
    {
        var probe = new FakeProbe { FilesExist = true };
        ClipboardKindClassifier.Classify("C:\\foo.txt", "C:\\foo.txt", probe).Should().Be(ContentKind.File);
    }

    [Fact]
    public void Classify_DirectoryPreferredOverFile()
    {
        var probe = new FakeProbe { FilesExist = true, DirsExist = true };
        ClipboardKindClassifier.Classify("C:\\foo", "C:\\foo", probe).Should().Be(ContentKind.Folder);
    }

    [Fact]
    public void Classify_NothingExists_ReturnsClip()
    {
        ClipboardKindClassifier.Classify("random text", null, NoFs).Should().Be(ContentKind.Clip);
    }

    [Fact]
    public void Classify_EmptyInput_ReturnsClip()
    {
        ClipboardKindClassifier.Classify(string.Empty, null, NoFs).Should().Be(ContentKind.Clip);
    }

    [Fact]
    public void Classify_NormalizedPathPreferredOverRaw()
    {
        var probe = new FakeProbe { DirsExist = true };
        ClipboardKindClassifier.Classify("typo", "C:\\corrected", probe).Should().Be(ContentKind.Folder);
    }
}
