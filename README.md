<div align="center">

<img src="docs/logo.png" alt="MyList" width="112" />

# MyList

**A keyboard-first launcher and list manager for Windows.**

Organize files, folders, multi-folder Explorer tab-groups, clipboard snippets, and runnable
scripts into collections — then open them from a global hotkey or the system tray.

<br/>

[![Latest release](https://img.shields.io/github/v/release/Sdomit/MyList?sort=semver&label=release&color=0078D6)](https://github.com/Sdomit/MyList/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Sdomit/MyList/total?color=2ea44f&label=downloads)](https://github.com/Sdomit/MyList/releases)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-0078D6)](#requirements)
[![License: MIT](https://img.shields.io/badge/license-MIT-yellow.svg)](LICENSE)

</div>

---

## ⬇️ Download

### **[Download MyList for Windows →](https://github.com/Sdomit/MyList/releases/latest/download/MyList-win-x64.zip)**

Extract `MyList-win-x64.zip` anywhere and run **`MyList.exe`**. Portable — no installer, and no
.NET runtime to install (it's self-contained).

> [!NOTE]
> Windows SmartScreen may warn about a new, unsigned app. Click **More info → Run anyway**.
> Keep `MyList.exe`, `desktops.png`, `icon.ico`, and the `icons` folder together.

---

## ✨ Features

- 🗂️ **Collections** of items, including smart collections (recent, favorites, …).
- 📁 **Many item types** — files, folders, *mtabs* (open several folders as Explorer tabs at once),
  clipboard text, clipboard images, and runnable action items (Command / Batch / PowerShell).
- ⚙️ **Per-item launch profiles** — arguments, working directory, run-as-admin, and terminal
  choice (Windows Terminal / PowerShell / cmd).
- ⌨️ **Global hotkey** to show/hide, with automatic fallback if the combo is taken.
- 🔔 **System tray** with show/hide, settings, and exit.
- 🎨 **Theming** — dark, light, or follow-system, plus selectable UI density.
- 🩺 **Path health checks** — items show missing / offline / permission-denied state, network-aware.
- 🧹 **Duplicate detection** and a duplicate manager.
- 🔎 **Command palette** for fast keyboard navigation.
- 🪟 **Explorer integration** — add open folders to an mtab; open mtabs as grouped tabs.
- ↩️ **Undo / redo**, drag & drop, and single-instance handling.
- 💾 **Backup / export / import** of schema-versioned data packages.

---

## 🚀 Getting started

1. Download and extract the [latest release](https://github.com/Sdomit/MyList/releases/latest), then run `MyList.exe`.
2. The window opens (and lives in the system tray). Add files/folders with drag & drop or the **+** button.
3. Group items into collections; press your **global hotkey** to summon the window from anywhere.
4. Open the **Command palette** for keyboard-only navigation, and tweak theme/hotkey/startup in **Settings**.

---

## 🖥️ Requirements

- Windows 10 or 11 (x64)
- Nothing else for the release build — the download is self-contained.
- To build from source: the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (`net8.0-windows`).

---

## 🛠️ Build from source

```powershell
git clone https://github.com/Sdomit/MyList.git
cd MyList
dotnet run --project Mylist/MyList.csproj          # build & launch
```

Produce a portable self-contained build:

```powershell
dotnet publish Mylist/MyList.csproj -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Or open `MyList.sln` in Visual Studio 2022 and run.

---

## 📂 Data & logs

Settings, data, backups, clipboard assets, and logs live under:

```
%AppData%\MyList
```

The diagnostics page (Settings → Diagnostics) surfaces hotkey, tray, network, and Explorer status,
plus a live log tail.

---

## 🗺️ Project layout

```
MyList.sln
Mylist/
  App.xaml(.cs)    App lifecycle, single-instance, global handlers
  Models/          Data models (items, collections, settings, undo actions)
  ViewModels/      MVVM view models (MainViewModel is the core)
  Views/           Windows and views (XAML + code-behind)
  Services/        Storage, launcher, hotkey, tray, theme, Explorer integration, …
  Helpers/         Commands, path normalization, search parsing
  Converters/      WPF value converters
  Resources/       Colors, density, and control styles
  icons/           Tray and action icons
```

---

## 🐛 Known issues

See [ISSUES.md](ISSUES.md) for the current backlog and the bugs already fixed.

## 📄 License

[MIT](LICENSE) © Sarmad Domit
