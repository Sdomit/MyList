# Known Issues

Findings from a full code review of the C# sources. Severity reflects user impact.

## Fixed in the initial commit

- **Launches crash / silently fail on missing paths** — `MainViewModel` open handlers (`OpenItem`, `OpenInNewWindow`, `OpenInTerminal`, `OpenAllItems`, `OpenSelectedItems`) now route every launch through a `TryLaunch` guard that logs and shows a status message instead of throwing out of an `async void`.
- **NetworkCheckService crashes** — the 30s timer tick was an unguarded `async void`; it enumerated the live `AllItems` collection (could throw *"collection was modified"*); and `RetryCheckAsync` wrote a UI-bound property off the UI thread. Now: tick wrapped in try/catch, items snapshotted before iteration, `HealthState` writes marshalled to the dispatcher, and the `CancellationTokenSource` is disposed instead of leaked.
- **System theme change crash** — `SystemThemeService` invoked its callback (which mutates WPF UI) on a non-UI thread. Now marshalled to the dispatcher.
- **Nullable-enum converter crash** — `EnumToBooleanConverter.ConvertBack` threw `Enum.Parse` for nullable-enum binding targets. Now unwraps `Nullable<T>` and uses `Enum.TryParse`.
- **Swallowed async-command errors** — `AsyncRelayCommand` had no catch around the awaited body; failures vanished. Now logged.
- **Resource leaks** — tray `ContextMenuStrip` is now disposed; `Process` handles from `LauncherService` / `ManagedActionRunnerService` are disposed.
- **Misleading hotkey status** — a fallback-registered hotkey could still report *"failed (Win32: 1409)"*; the error code is no longer clobbered.
- **Duplicate event subscription** — `MainWindow.OnLoaded` re-subscribed `Settings.PropertyChanged` on every re-show; now guarded.

## Open — High

- **StorageService export shares live references** — `CreateExportClone` reuses the live `Collections`/`Items` instances, so serialization can race with UI-thread mutation. Snapshot/deep-clone before writing.
- **StorageService.RestoreLatestBackup is sync-over-async** — `.GetAwaiter().GetResult()` on a path that can run on the UI thread. Make it async.
- **Stale undo indices for bulk operations** — `DeleteSelectedItemsGlobally` and `MoveSelectedToCollection` snapshot `IndexOf` before the composite runs, so undo reinserts items at wrong positions. Compute restore indices at revert time.
- **Explorer automation freezes the UI** — `ExplorerTabAutomationService` runs `SendKeys.SendWait` on the dispatcher thread; multi-folder opens block the UI. Move to a dedicated STA thread.

## Open — Medium

- **App swallows all UI exceptions** — `App.DispatcherUnhandledException` sets `Handled = true` unconditionally, masking crashes and leaving the app in a possibly-corrupt state. Handle selectively.
- **DiagnosticsViewModel timer always on** — a 3s `DispatcherTimer` reads the log file and rebuilds a collection for the whole session even when diagnostics is closed. Gate on visibility.
- **MtabEditorWindow disk I/O per keystroke** — `Directory.Exists` runs on the UI thread on every character. Debounce or validate off-thread.
- **Search box accepts control characters** — `MainWindow.OnWindowPreviewTextInput` appends raw `e.Text`; filter with `char.IsControl`.
- **Stuck drag cursor** — `Mouse.OverrideCursor` can remain set if a drag ends without hitting the hide path.
- **Unbounded icon cache** — `IconService._cache` never evicts; long sessions grow memory.
- **Path normalization** — incomplete UNC paths (`\\server`, no share) are accepted; the path key is unstable when `Path.GetFullPath` fails on an offline network path.
- **InverseBooleanConverter** echoes non-bool/null values to a `bool` target, producing wrong enable-state.
- **ManagedActionRunnerService** — generated runtime scripts are never cleaned up; `LaunchProfile` is dereferenced without a null check.
- **ClipboardAssetService** — a partial `.png` can be left on IO failure; `Clipboard.SetImage` `COMException` is not caught.
- **StartupService** — registry write failures are silently swallowed; a null executable path writes a broken Run entry.

## Open — Low

- Several dialogs set `Owner = Application.Current.MainWindow` without a null check (throws during shutdown).
- `LogService.ReadTail` can transiently fail under concurrent append (sharing violation).
- Minor collection-name suffix parsing collisions (`Name_01` vs `Name_1`).
- `MultiplyValueConverter` silently yields a zero `Thickness` for a 3-value parameter.

> The fixed items above were verified by a clean `dotnet build` (0 warnings, 0 errors). Open items were reviewed but not changed in the first commit.
