# MyList

A keyboard-first launcher and list manager for Windows. Organize files, folders, multi-folder Explorer tab groups, clipboard snippets, and runnable command/script actions into collections, then open them from a global hotkey or the system tray.

Built with WPF on .NET 8 (Windows).

## Features

- **Collections** of items, including smart collections (recent, favorites, etc.).
- **Item types**: files, folders, *mtabs* (open several folders as Explorer tabs at once), clipboard text, clipboard images, and action items (Command / Batch / PowerShell).
- **Launch profiles** per item: arguments, working directory, run-as-admin, and terminal profile (Windows Terminal / PowerShell / cmd).
- **Global hotkey** to show/hide the window, with automatic fallback if the chosen combo is taken.
- **System tray** with show/hide, settings, and exit.
- **Theming**: dark, light, or follow-system, plus selectable UI density.
- **Path health checks**: items show missing / offline / permission-denied state, with network-path awareness.
- **Duplicate detection** and a duplicate manager.
- **Command palette** for fast keyboard navigation.
- **Windows Explorer integration**: add open folders to an mtab; open mtabs as grouped Explorer tabs.
- **Drag & drop**, undo/redo, and single-instance handling.
- **Backup / export / import** of schema-versioned data packages.

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (the project targets `net8.0-windows`)

## Build & run

From the repository root:

```powershell
dotnet build MyList.sln -c Release
dotnet run --project Mylist/MyList.csproj
```

Or open `MyList.sln` in Visual Studio 2022 and run.

To produce a self-contained build:

```powershell
dotnet publish Mylist/MyList.csproj -c Release -r win-x64 --self-contained
```

## Data location

Settings, data, logs, and clipboard assets are stored under `%AppData%\MyList`.

## Project layout

```
MyList.sln
Mylist/
  App.xaml(.cs)        App lifecycle, single-instance, global handlers
  Models/              Data models (items, collections, settings, undo actions)
  ViewModels/          MVVM view models (MainViewModel is the core)
  Views/               Windows and views (XAML + code-behind)
  Services/            Storage, launcher, hotkey, tray, theme, Explorer integration, etc.
  Helpers/             Commands, path normalization, search parsing
  Converters/          WPF value converters
  Resources/           Colors, density, and control styles
  icons/               Tray and action icons
```

## Known issues

See [ISSUES.md](ISSUES.md) for the current bug backlog (and what was fixed in the first commit).
