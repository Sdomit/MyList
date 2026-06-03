using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MyList.Models;

namespace MyList.Services;

public sealed class LogService
{
    private static readonly Lazy<LogService> Lazy = new(() => new LogService());
    public static LogService Instance => Lazy.Value;

    private readonly object _lock = new();

    private LogService()
    {
    }

    public void Log(string message)
    {
        WriteLine($"{DateTime.UtcNow:O} | {message}");
    }

    public void Log(Exception ex, string? message = null)
    {
        var line = message is null
            ? $"{DateTime.UtcNow:O} | {ex}"
            : $"{DateTime.UtcNow:O} | {message} | {ex}";
        WriteLine(line);
    }

    private void WriteLine(string line)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MyList",
                "logs");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "app.log");
            lock (_lock)
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch
        {
            // Swallow logging failures.
        }
    }

    public IReadOnlyList<string> ReadTail(int lineCount)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MyList",
                "logs");
            var path = Path.Combine(folder, "app.log");
            if (!File.Exists(path))
            {
                return Array.Empty<string>();
            }

            var lines = new List<string>();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    lines.Add(line);
                }
            }

            return lines.TakeLast(Math.Max(1, lineCount)).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public string BuildDebugReport(int tailLines = 120)
    {
        var lines = ReadTail(Math.Max(10, tailLines));
        var sb = new StringBuilder();
        sb.AppendLine("MyList Debug Report");
        sb.AppendLine($"Generated (UTC): {DateTime.UtcNow:O}");
        sb.AppendLine($"Debug mode: {RuntimeStatus.DebugMode}");
        sb.AppendLine($"Log file: {RuntimeStatus.DebugLogFilePath}");
        sb.AppendLine($"Hotkey status: {RuntimeStatus.HotkeyStatusMessage}");
        sb.AppendLine($"Effective hotkey: {RuntimeStatus.EffectiveHotkey}");
        sb.AppendLine($"Tray: {(RuntimeStatus.TrayInitialized ? (RuntimeStatus.TrayVisible ? "Initialized / Visible" : "Initialized / Hidden") : "Not initialized")}");
        sb.AppendLine($"Network: {(RuntimeStatus.NetworkLastRunUtc is null ? "No checks yet" : $"Last {RuntimeStatus.NetworkLastRunUtc:O}, pending {RuntimeStatus.NetworkPendingCount}")}");
        sb.AppendLine("Recent logs:");

        foreach (var line in lines)
        {
            sb.AppendLine(line);
        }

        return sb.ToString();
    }
}
