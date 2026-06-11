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

## Fixed: Explorer STA thread — merged to main (runtime verification pending)

- **Explorer automation froze the UI** — `ExplorerTabAutomationService` ran `SendKeys.SendWait` (plus WPF Clipboard and Shell COM) on the dispatcher thread, so multi-folder mtab opens blocked the UI. The whole flow now runs on a dedicated background **STA** thread (`RunOnStaThreadAsync`); the async/await chain was converted to synchronous, cancellable sleeps. Public method signatures are unchanged, so callers still `await` and resume on the UI thread for result handling. Merged to main; runtime verification against real Explorer windows is still owed (interactive, GUI-only).

## Fixed: theme runtime application — UI walkthrough (verified live)

### High
- **Theme / accent change did nothing until restart** — `ThemeService.ApplyTheme` swapped the merged color `ResourceDictionary`, but the `Token.*Brush` definitions in `Themes/Tokens.xaml` resolve their `Color` via `DynamicResource` against that *sibling* dictionary. An already-instantiated brush does not re-resolve when only the sibling is swapped, so a runtime toggle was a silent no-op — the change persisted to `data.json` but the live window never updated (startup worked only because `ApplyTheme` runs before any window is realized, `App.xaml.cs:119`). Fix: after swapping the color dictionary, `ApplyTheme` now rebuilds the brush layer (re-inserts `Tokens.xaml`) so realized windows re-resolve. Verified live — Dark↔Light and accent now switch instantly.
- **Light theme left the window chrome dark and unreadable** — the window, header, and left sidebar bind `Token.BgMicaBrush`, whose `Token.BgMicaColor` (`#1A1D23`) was defined only in `Themes/Tokens.xaml:54` and overridden in **0 of 8** `Colors.Light.*.xaml`. In light mode the chrome stayed dark navy while content went light, and the dark light-mode text on it was invisible (collection names, tool names). Fix: each `Colors.Light.*.xaml` now defines a light `Token.BgMicaColor` (its near-white canvas tone). Verified live — light-mode sidebar/header are light with legible text.

## Fixed: Low-severity safety pass

- **Dialog `Owner` null safety** — `MainViewModel` now routes 15 dialog-open
  sites through a `GetOwnerWindow()` helper that returns `Application.Current?.MainWindow`,
  so shutdown-race NREs can't surface.
- **StorageService.ReplaceFile fallback** — the cross-volume / network-share
  path swapped delete-then-move for `File.Move(temp, target, overwrite: true)`,
  closing the window where the target file was missing on crash or for
  concurrent readers.
- **MultiplyValueConverter** — malformed `ConverterParameter` (e.g. a 3-value
  list) now returns `DependencyProperty.UnsetValue` instead of a zero
  `Thickness`, so WPF falls back to the property default rather than silently
  flattening padding.

## Fixed: previously-listed Open items, confirmed already addressed in code

These were carried as Open until a re-audit against current code confirmed each
had already been resolved by an earlier merged PR. Verified line references:

- **Mini-launcher orbit reacts to the search query** — `MiniLauncherViewModel.Rebuild`
  re-derives both the indexed list and the orbit center/satellites from the
  filtered `matches` set, falling back to favorites / recents only when the
  query is empty (`MiniLauncherViewModel.cs:110-160`).
- **"Last opened" shows "Never" for unopened items** — `ItemModel.LastOpenedDisplay`
  returns `"Never"` when `_lastOpenedDate == DateTime.MinValue`
  (`Mylist/Models/ItemModel.cs:173-174`).
- **Grid view item cards no longer bottom-anchor** — the `UniformGrid` items
  panel sets `VerticalAlignment="Top"` so a single row of cards sizes to the
  cards instead of stretching to the viewport (`Mylist/Views/MainWindow.xaml:1145`).
- **Settings "Dark-first tokens active" debug chip removed** — the sidebar
  `Border` that rendered the literal status banner was deleted in the UI
  polish pass (no remaining occurrence in `SettingsView.xaml`).

## Fixed: Mini-launcher dead-path safety

- **Mini-launcher silently dismissed on dead paths** — both `ActivateItemCommand`
  and `OpenIndexedItemCommand` now route through a `TryLaunch` guard that
  checks `ItemHealthState` before launching: `Missing` / `Offline` items keep
  the launcher open and surface a status message ("Path not found: …" or
  "Offline: …") in the footer in the warning-status color, and an unexpected
  exception from `LauncherService.Open` is caught and surfaced the same way.
  The launcher only dismisses on a successful launch
  (`MiniLauncherViewModel.cs:38-65, 99-138`, `MiniLauncherWindow.xaml` footer).

## Open

### Low
- **Path health flaps** — the same unreachable UNC item cycles `Offline` ↔ `Missing` ↔ healthy across re-checks within one session. Likely root cause: `NetworkCheckService.EvaluatePathStateAsync` distinguishes Missing from Offline by the exception type thrown by `Directory.Exists` / `File.Exists` against an SMB target with intermittent reachability, and the OS swaps the underlying error code between probes. Needs a stable Offline detector (e.g. ping the host once per cycle and short-circuit Missing) before this can be cleanly closed.

> All fixes verified by a clean `dotnet build` (0 warnings, 0 errors). The two theme fixes were additionally verified live in the running app: Dark↔Light and accent switch instantly, and the light-mode sidebar/header render light with legible text.
