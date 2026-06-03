# Known Issues

Findings from a full code review of the C# sources. Severity reflects user impact.

## Fixed in the initial commit

- **Launches crash / silently fail on missing paths** — `MainViewModel` open handlers now route every launch through a `TryLaunch` guard that logs and shows a status message instead of throwing out of an `async void`.
- **NetworkCheckService crashes** — unguarded `async void` timer tick, live-collection enumeration, and off-UI-thread `HealthState` writes. Now: tick wrapped, items snapshotted, writes marshalled, `CancellationTokenSource` disposed.
- **System theme change crash** — `SystemThemeService` callback now marshalled to the dispatcher.
- **Nullable-enum converter crash** — `EnumToBooleanConverter.ConvertBack` now unwraps `Nullable<T>` and uses `Enum.TryParse`.
- **Swallowed async-command errors** — `AsyncRelayCommand` now logs failures.
- **Resource leaks** — tray `ContextMenuStrip` and `Process` handles disposed.
- **Misleading hotkey status** — error code no longer clobbered on fallback registration.
- **Duplicate event subscription** — `MainWindow.OnLoaded` guarded.

## Fixed in the follow-up pass

### High
- **Export race** — `StorageService.ExportCollectionsAsync` now builds a deep clone (JSON round-trip) on the calling UI thread *before* the gate `await`, so package serialization can't race with live-collection mutation.
- **Sync-over-async restore** — `RestoreLatestBackup()` is now `RestoreLatestBackupAsync()`; the caller awaits it.
- **Stale undo indices (bulk delete / move)** — actions now capture positions at **apply** time, so the reverse-order composite revert restores each item to its original slot. Moves also append using the `Entries` index space (fixes the separator/`ItemIds` mismatch).

### Medium
- **App swallowed all UI exceptions** — `DispatcherUnhandledException` still logs and stays resilient, but now surfaces a message box when debug mode is on (no longer fully silent).
- **DiagnosticsViewModel timer always on** — the 3s refresh timer is now started/stopped on the view's `Loaded`/`Unloaded`, so it doesn't read the log file in the background when diagnostics is closed.
- **MtabEditorWindow UI block** — UNC paths are accepted without `Directory.Exists` (no block on offline shares) and validation is debounced 250 ms instead of running on every keystroke.
- **Unbounded icon cache** — `IconService` cache is now capped (512, FIFO eviction).
- **Incomplete UNC accepted** — `PathNormalizationHelper` now requires server *and* share.
- **InverseBooleanConverter** — returns `Binding.DoNothing` for non-bool input and implements `ConvertBack` (no `NotSupportedException`).
- **ManagedActionRunnerService** — stale runtime scripts (>1h) are swept before each run; `ItemModel.LaunchProfile` is null-hardened at the setter.
- **ClipboardAssetService** — partial `.png` cleaned up on save failure; `SetImage` wrapped; a single bad asset no longer aborts the export/import batch.
- **StartupService** — failures are logged instead of swallowed; empty executable path guarded.

### Low
- **LogService.ReadTail** opens with `FileShare.ReadWrite` (no sharing violation under concurrent append).
- **MainWindow** — control characters filtered from the search box; drag cursor/flag reset guaranteed via `try/finally` even if `DoDragDrop` throws.

## Fixed on branch `fix/explorer-sta-thread` (pending interactive verification)

- **Explorer automation froze the UI** — `ExplorerTabAutomationService` ran `SendKeys.SendWait` (plus WPF Clipboard and Shell COM) on the dispatcher thread, so multi-folder mtab opens blocked the UI. The whole flow now runs on a dedicated background **STA** thread (`RunOnStaThreadAsync`); the async/await chain was converted to synchronous, cancellable sleeps. Public method signatures are unchanged, so callers still `await` and resume on the UI thread for result handling. Needs hands-on testing against real Explorer windows before merge.

## Open

### Low
- Some dialogs set `Owner = Application.Current.MainWindow` without a null check (throws only in rare shutdown races).
- `StorageService.ReplaceFile` fallback (for providers without atomic replace) deletes-then-moves; the primary path uses atomic `File.Replace`.
- `MultiplyValueConverter` yields a zero `Thickness` for a malformed 3-value `ConverterParameter`.

> All fixes verified by a clean `dotnet build` (0 warnings, 0 errors).
