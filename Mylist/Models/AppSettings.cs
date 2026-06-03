using System;

namespace MyList.Models;

public sealed class AppSettings
{
    public bool FollowSystemTheme { get; set; } = true;
    public ThemeMode Theme { get; set; } = ThemeMode.Light;
    public ViewMode ViewMode { get; set; } = ViewMode.Grid;
    public LayoutMode LayoutMode { get; set; } = LayoutMode.Resizable;
    public CollectionsLayout CollectionsLayout { get; set; } = CollectionsLayout.Tabs;
    public bool AlwaysOnTop { get; set; }
    public bool AutoHide { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public HotkeySettings GlobalHotkey { get; set; } = new();
    public UiDensity UiDensity { get; set; } = UiDensity.Comfortable;
    public double ItemScale { get; set; } = 1.0;
    public bool EnableDebugMode { get; set; }
    public WindowPlacement WindowPlacement { get; set; } = new();
    public DateTime LastBackupDate { get; set; } = DateTime.MinValue;
}
