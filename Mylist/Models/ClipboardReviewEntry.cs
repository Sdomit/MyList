namespace MyList.Models;

public enum ClipboardReviewStatus
{
    ValidNew,
    ValidExisting,
    DuplicateInClipboard,
    Invalid
}

public sealed class ClipboardReviewEntry
{
    public string RawInput { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public ClipboardReviewStatus Status { get; init; }
    public bool WillApply { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string PathKey { get; init; } = string.Empty;
    public ContentKind Kind { get; init; } = ContentKind.Clip;

    public string DisplayPath => string.IsNullOrWhiteSpace(Path) ? RawInput : Path;
}
