using System;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using MahApps.Metro.IconPacks;
using MyList.Models;

namespace MyList.ViewModels;

public interface IPaletteRow
{
    string Title { get; }
    string? Subtitle { get; }
    PackIconLucideKind LucideKind { get; }
    string? Shortcut { get; }
    ContentKind? Kind { get; }
    Brush? BadgeBackground { get; }
    string? BadgeLabel { get; }
    bool KeepPaletteOpen { get; }
    bool IsOverflow { get; }
    void Execute();
}

public sealed class ItemRow : IPaletteRow
{
    private readonly ItemModel _item;
    private readonly Action<ItemModel> _open;

    public ItemRow(ItemModel item, Action<ItemModel> open)
    {
        _item = item;
        _open = open;
    }

    public ItemModel Item => _item;
    public string Title => _item.Name;
    public string? Subtitle => _item.DisplayPath;
    public PackIconLucideKind LucideKind => ResolveKind(_item);
    public string? Shortcut => null;
    public ContentKind? Kind => _item.Kind;
    public Brush? BadgeBackground => null;
    public string? BadgeLabel => null;
    public bool KeepPaletteOpen => false;
    public bool IsOverflow => false;

    public void Execute() => _open(_item);

    private static PackIconLucideKind ResolveKind(ItemModel item)
    {
        if (item.IsActionItem) return PackIconLucideKind.Terminal;
        if (item.IsMtab) return PackIconLucideKind.Folders;
        if (item.IsClipboardImage) return PackIconLucideKind.Image;
        if (item.IsClipboardText) return PackIconLucideKind.Clipboard;
        return item.Type switch
        {
            ItemType.Folder => PackIconLucideKind.Folder,
            ItemType.App => PackIconLucideKind.AppWindow,
            _ => PackIconLucideKind.File,
        };
    }
}

public sealed class CommandRow : IPaletteRow
{
    public string Title { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public PackIconLucideKind LucideKind { get; init; } = PackIconLucideKind.Zap;
    public string? Shortcut { get; init; }
    public ContentKind? Kind => null;
    public Brush? BadgeBackground => null;
    public string? BadgeLabel => null;
    public bool KeepPaletteOpen { get; init; }
    public bool IsOverflow => false;
    public Action ExecuteAction { get; init; } = () => { };
    public void Execute() => ExecuteAction();
}

public sealed class SettingsRow : IPaletteRow
{
    public string Title { get; init; } = string.Empty;
    public string? Subtitle { get; init; } = "Settings";
    public PackIconLucideKind LucideKind { get; init; } = PackIconLucideKind.Settings;
    public string? Shortcut { get; init; }
    public ContentKind? Kind => null;
    public Brush? BadgeBackground => Application.Current?.TryFindResource("Token.BgMutedBrush") as Brush;
    public string? BadgeLabel => "S";
    public bool KeepPaletteOpen => false;
    public bool IsOverflow => false;
    public string AnchorKey { get; init; } = string.Empty;
    public Action ExecuteAction { get; init; } = () => { };
    public void Execute() => ExecuteAction();
}

public sealed class OverflowRow : IPaletteRow
{
    public string Title { get; init; } = string.Empty;
    public string? Subtitle => null;
    public PackIconLucideKind LucideKind => PackIconLucideKind.Ellipsis;
    public string? Shortcut => null;
    public ContentKind? Kind => null;
    public Brush? BadgeBackground => null;
    public string? BadgeLabel => null;
    public bool KeepPaletteOpen => false;
    public bool IsOverflow => true;
    public void Execute() { }
}
