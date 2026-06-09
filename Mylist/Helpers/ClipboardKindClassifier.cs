using System;
using MyList.Models;

namespace MyList.Helpers;

public static class ClipboardKindClassifier
{
    public static ContentKind Classify(string? rawInput, string? normalizedPath, IFileSystemProbe probe)
    {
        var input = (rawInput ?? string.Empty).Trim();
        if (input.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
            || input.StartsWith("cmd:", StringComparison.OrdinalIgnoreCase)
            || input.StartsWith("run:", StringComparison.OrdinalIgnoreCase))
        {
            return ContentKind.Action;
        }

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return ContentKind.Mtab;
        }

        var candidate = string.IsNullOrWhiteSpace(normalizedPath) ? input : normalizedPath;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return ContentKind.Clip;
        }

        if (probe.DirectoryExists(candidate))
        {
            return ContentKind.Folder;
        }

        if (probe.FileExists(candidate))
        {
            return ContentKind.File;
        }

        return ContentKind.Clip;
    }
}
