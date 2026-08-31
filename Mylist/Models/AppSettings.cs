using System;
using System.Windows.Input;

namespace MyList.Models;

public sealed class AppSettings
{
    public bool FollowSystemTheme { get; set; } = true;
    public ThemeMode Theme { get; set; } = ThemeMode.Light;
    public AccentPalette Accent { get; set; } = AccentPalette.Blue;
    public ViewMode ViewMode { get; set; } = ViewMode.Grid;
    public LayoutMode LayoutMode { get; set; } = LayoutMode.Resizable;
    public CollectionsLayout CollectionsLayout { get; set; } = CollectionsLayout.Tabs;
    public bool AlwaysOnTop { get; set; }
    public bool AutoHide { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithWindows { get; set; }
    // Kept off by default so existing double-click behaviour remains unchanged.
    public bool OpenItemsOnSingleClick { get; set; }
    public HotkeySettings GlobalHotkey { get; set; } = new();

    public HotkeySettings MiniLauncherHotkey { get; set; } = new()
    {
        Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
        Key = Key.G,
    };

    // Smart-view ids (recent/fav/trending/pinned) the user hid from the quick menu.
    public List<string> HiddenQuickMenuViews { get; set; } = new();

    // Launchable items per mini-launcher ring. The "More…" slot is added separately.
    public int MiniLauncherItemLimit { get; set; } = 5;

    public UiDensity UiDensity { get; set; } = UiDensity.Comfortable;
    public UiSkin Skin { get; set; } = UiSkin.Windows11;
    public double ItemScale { get; set; } = 1.0;
    public bool EnableDebugMode { get; set; }
    public WindowPlacement WindowPlacement { get; set; } = new();
    public DateTime LastBackupDate { get; set; } = DateTime.MinValue;
}
