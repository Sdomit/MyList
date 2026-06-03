using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace MyList.Helpers;

public enum PathValidationStatus
{
    Valid,
    Empty,
    InvalidFormat,
    NotRooted
}

public static class PathNormalizationHelper
{
    private static readonly Regex QuotedPathRegex = new(
        "\"((?:[A-Za-z]:\\\\|\\\\\\\\)[^\"\\r\\n]+)\"",
        RegexOptions.Compiled);

    private static readonly Regex InlinePathRegex = new(
        "(?<![A-Za-z0-9_])((?:[A-Za-z]:\\\\|\\\\\\\\)[^\\s\"'<>|]+)",
        RegexOptions.Compiled);

    private static readonly Regex PowerShellPromptPathRegex = new(
        "^\\s*(?:PS|pwsh)\\s+((?:[A-Za-z]:\\\\|\\\\\\\\).+?)\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CmdPromptPathRegex = new(
        "^\\s*((?:[A-Za-z]:\\\\|\\\\\\\\).+?)\\s*>",
        RegexOptions.Compiled);

    public static IEnumerable<string> ExtractClipboardCandidates(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (var line in normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryExtractPromptPath(line, out var promptPath))
            {
                var promptCandidate = CleanCandidate(promptPath);
                if (!string.IsNullOrWhiteSpace(promptCandidate) && seen.Add(promptCandidate))
                {
                    yield return promptCandidate;
                }
            }

            var cleaned = CleanCandidate(line);
            if (!string.IsNullOrWhiteSpace(cleaned) && seen.Add(cleaned))
            {
                yield return cleaned;
            }

            foreach (Match match in QuotedPathRegex.Matches(line))
            {
                var quotedCandidate = CleanCandidate(match.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(quotedCandidate) && seen.Add(quotedCandidate))
                {
                    yield return quotedCandidate;
                }
            }

            foreach (Match match in InlinePathRegex.Matches(line))
            {
                var inlineCandidate = CleanCandidate(match.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(inlineCandidate) && seen.Add(inlineCandidate))
                {
                    yield return inlineCandidate;
                }
            }
        }
    }

    public static string? CleanCandidate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        if (cleaned.Length >= 2 && cleaned.StartsWith('"') && cleaned.EndsWith('"'))
        {
            cleaned = cleaned.Substring(1, cleaned.Length - 2).Trim();
        }

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return null;
        }

        return cleaned.Replace('/', '\\');
    }

    public static bool IsValidPathCandidate(string? value)
    {
        var cleaned = CleanCandidate(value);
        if (cleaned is null)
        {
            return false;
        }

        if (cleaned.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return HasUncServerAndShare(cleaned);
        }

        return Path.IsPathRooted(cleaned);
    }

    public static bool TryNormalizeUserPath(string? value, out string normalizedPath, out string pathKey)
    {
        return TryNormalizeUserPath(value, out normalizedPath, out pathKey, out _);
    }

    public static bool TryNormalizeUserPath(string? value, out string normalizedPath, out string pathKey, out PathValidationStatus validationStatus)
    {
        normalizedPath = string.Empty;
        pathKey = string.Empty;
        validationStatus = PathValidationStatus.Empty;

        var cleaned = CleanCandidate(value);
        if (cleaned is null)
        {
            validationStatus = PathValidationStatus.Empty;
            return false;
        }

        if (cleaned.StartsWith(@"\\", StringComparison.Ordinal))
        {
            if (!HasUncServerAndShare(cleaned))
            {
                validationStatus = PathValidationStatus.InvalidFormat;
                return false;
            }
        }
        else if (!Path.IsPathRooted(cleaned))
        {
            validationStatus = PathValidationStatus.NotRooted;
            return false;
        }

        if (!IsValidPathCandidate(cleaned))
        {
            validationStatus = PathValidationStatus.InvalidFormat;
            return false;
        }

        normalizedPath = NormalizePath(cleaned);
        pathKey = BuildPathKey(normalizedPath);
        validationStatus = PathValidationStatus.Valid;
        return true;
    }

    public static string? GetPathKey(string? value)
    {
        if (!TryNormalizeUserPath(value, out _, out var key))
        {
            return null;
        }

        return key;
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Replace('/', '\\');
        try
        {
            if (Path.IsPathRooted(normalized))
            {
                normalized = Path.GetFullPath(normalized);
            }
        }
        catch
        {
            // Keep the original path if GetFullPath fails for unavailable network paths.
        }

        return TrimTrailingSeparator(normalized);
    }

    private static string BuildPathKey(string normalizedPath)
    {
        return TrimTrailingSeparator(normalizedPath).ToUpperInvariant();
    }

    private static string TrimTrailingSeparator(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        var value = path;

        if (IsDriveRoot(value) || IsUncShareRoot(value))
        {
            return value;
        }

        while (value.Length > 1 && (value.EndsWith("\\", StringComparison.Ordinal) || value.EndsWith("/", StringComparison.Ordinal)))
        {
            value = value.Substring(0, value.Length - 1);
            if (IsDriveRoot(value) || IsUncShareRoot(value))
            {
                break;
            }
        }

        return value;
    }

    private static bool IsDriveRoot(string path)
    {
        return path.Length == 3
               && char.IsLetter(path[0])
               && path[1] == ':'
               && path[2] == '\\';
    }

    private static bool IsUncShareRoot(string path)
    {
        if (!path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return false;
        }

        var trimmed = path.TrimEnd('\\');
        var parts = trimmed.Substring(2).Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2;
    }

    private static bool HasUncServerAndShare(string path)
    {
        if (!path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return false;
        }

        var trimmed = path.TrimEnd('\\');
        if (trimmed.Length <= 2)
        {
            return false;
        }

        var parts = trimmed.Substring(2).Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2;
    }

    private static bool TryExtractPromptPath(string line, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var psMatch = PowerShellPromptPathRegex.Match(line);
        if (psMatch.Success)
        {
            path = psMatch.Groups[1].Value.Trim();
            return true;
        }

        var cmdMatch = CmdPromptPathRegex.Match(line);
        if (cmdMatch.Success)
        {
            path = cmdMatch.Groups[1].Value.Trim();
            return true;
        }

        return false;
    }
}
