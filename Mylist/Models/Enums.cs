using System.Windows.Input;

namespace MyList.Models;

public enum ItemType
{
    Folder,
    File,
    App
}

public enum ActionKind
{
    None,
    Command,
    Batch,
    PowerShell
}

public enum CollectionEntryKind
{
    Item,
    Separator
}

public enum ViewMode
{
    Grid,
    List
}

public enum LayoutMode
{
    Docked,
    Floating,
    Resizable
}

public enum CollectionsLayout
{
    Tabs,
    Accordion,
    Sidebar
}

public enum SmartCollectionType
{
    None,
    Tag,
    Recent
}

public enum ThemeMode
{
    Light,
    Dark
}

public enum AccentPalette
{
    Amber,
    Blue,
    Green,
    Violet,
    Teal,
    Terracotta,
    Crimson,
    Slate
}

public enum WindowDockEdge
{
    Left,
    Right
}

[System.Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}
