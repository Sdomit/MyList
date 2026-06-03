using System;
using System.Collections.Generic;
using System.Linq;
using MyList.Models;

namespace MyList.Helpers;

public sealed class SearchQuery
{
    public List<string> FreeTerms { get; } = new();
    public List<string> Tags { get; } = new();
    public List<string> PathTerms { get; } = new();
    public ItemType? ItemType { get; set; }
    public bool? IsOffline { get; set; }
    public bool? IsFavorite { get; set; }
    public List<string> InvalidTokens { get; } = new();

    public bool IsEmpty =>
        FreeTerms.Count == 0 &&
        Tags.Count == 0 &&
        PathTerms.Count == 0 &&
        ItemType is null &&
        IsOffline is null &&
        IsFavorite is null;
}

public static class SearchQueryParser
{
    public static SearchQuery Parse(string? input)
    {
        var query = new SearchQuery();
        if (string.IsNullOrWhiteSpace(input))
        {
            return query;
        }

        foreach (var token in SplitTokens(input))
        {
            if (!token.Contains(':'))
            {
                query.FreeTerms.Add(token);
                continue;
            }

            var parts = token.Split(':', 2);
            var key = parts[0].Trim().ToLowerInvariant();
            var value = parts[1].Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                query.InvalidTokens.Add(token);
                continue;
            }

            switch (key)
            {
                case "tag":
                    query.Tags.Add(value);
                    break;
                case "path":
                    query.PathTerms.Add(value);
                    break;
                case "type":
                    if (TryParseType(value, out var type))
                    {
                        query.ItemType = type;
                    }
                    else
                    {
                        query.InvalidTokens.Add(token);
                    }
                    break;
                case "offline":
                    if (TryParseBoolean(value, out var offline))
                    {
                        query.IsOffline = offline;
                    }
                    else
                    {
                        query.InvalidTokens.Add(token);
                    }
                    break;
                case "fav":
                case "favorite":
                    if (TryParseBoolean(value, out var favorite))
                    {
                        query.IsFavorite = favorite;
                    }
                    else
                    {
                        query.InvalidTokens.Add(token);
                    }
                    break;
                default:
                    query.InvalidTokens.Add(token);
                    break;
            }
        }

        return query;
    }

    private static IEnumerable<string> SplitTokens(string input)
    {
        return input
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value));
    }

    private static bool TryParseBoolean(string value, out bool result)
    {
        if (bool.TryParse(value, out result))
        {
            return true;
        }

        if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "y", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "n", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }

    private static bool TryParseType(string value, out ItemType type)
    {
        if (Enum.TryParse<ItemType>(value, true, out type))
        {
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "folder":
            case "dir":
            case "directory":
                type = ItemType.Folder;
                return true;
            case "file":
                type = ItemType.File;
                return true;
            case "app":
            case "exe":
            case "application":
                type = ItemType.App;
                return true;
            default:
                type = default;
                return false;
        }
    }
}
