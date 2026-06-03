using System;
using System.IO;
using MyList.Models;

namespace MyList.Services;

public static class RuntimeStatus
{
    public static bool TrayInitialized { get; set; }
    public static bool TrayVisible { get; set; }

    public static bool HotkeyRegistered { get; set; }
    public static string HotkeyStatusMessage { get; set; } = "Not registered.";
    public static HotkeySettings EffectiveHotkey { get; set; } = new();
    public static int HotkeyErrorCode { get; set; }

    public static DateTime? NetworkLastRunUtc { get; set; }
    public static int NetworkPendingCount { get; set; }

    public static bool DebugMode { get; set; }

    public static bool ExplorerMenuInstalled { get; set; }
    public static string ExplorerMenuStatusMessage { get; set; } = "Unknown";
    public static DateTime? ExplorerMenuLastOperationUtc { get; set; }

    public static string DebugLogFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MyList",
        "logs",
        "app.log");
}
