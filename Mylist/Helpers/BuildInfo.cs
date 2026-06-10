using System;
using System.Reflection;

namespace MyList.Helpers;

public static class BuildInfo
{
    public static string DisplayString { get; } = ComputeDisplayString();

    private static string ComputeDisplayString()
    {
        var raw = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

        var plus = raw.IndexOf('+');
        if (plus < 0)
        {
            return raw;
        }

        var version = raw[..plus];
        var rest = raw[(plus + 1)..];
        var sha = rest.Length >= 7 ? rest[..7] : rest;
        return string.IsNullOrEmpty(sha) ? version : $"{version} · {sha}";
    }
}
