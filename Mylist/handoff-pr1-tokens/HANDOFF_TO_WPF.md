# MyList — Constellation → WPF re-handoff

**Source of truth:** `design_handoff_mylist_redesign/MyList - redesign.html` on the Forma design system.
**Target:** `Mylist/` WPF project (.NET 8, `net8.0-windows`).
**Why this doc:** the prior handoff invented an ad-hoc token system. Now the prototype runs on real Forma tokens, so every visual maps 1:1 to a `Token.*` key already defined in `Themes/Tokens.xaml`. No more drift.

---

## 0. The new ground rules

1. **Tokens are the only visual contract.** No hex colors, no inline font names, no magic numbers in views. If a view needs a value that isn't in `Token.*`, that's a missing token — add it to `Tokens.xaml` first.
2. **One theme attribute → one resource dictionary.** Forma uses `data-theme="dark-blue"`, `"light-amber"`, etc. WPF mirrors this with `Colors.{Theme}.{Accent}.xaml` — only the *semantic* layer changes between dictionaries; primitives are shared.
3. **Plus Jakarta Sans + JetBrains Mono.** Bundle as `.ttf` in `Mylist/Assets/Fonts/` and reference via pack URI; do not rely on Google Fonts at runtime.
4. **No emoji, no unicode bullets, no decorative gradients.** All glyphs come from the icon sprite.

---

## 1. Token map — Forma CSS → `Token.*` (WPF)

### 1.1 Color

| Forma CSS | WPF `Color` key | WPF `Brush` key |
|---|---|---|
| `--accent` | `Token.AccentColor` | `Token.AccentBrush` |
| `--accent-hover` | `Token.AccentHoverColor` | `Token.AccentHoverBrush` |
| `--accent-active` | *(add)* `Token.AccentActiveColor` | `Token.AccentActiveBrush` |
| `--accent-subtle` | `Token.AccentSoftColor` (rename → `Token.AccentSubtleColor`) | `Token.AccentSubtleBrush` |
| `--accent-muted` | `Token.AccentMuteColor` (rename → `Token.AccentMutedColor`) | `Token.AccentMutedBrush` |
| `--accent-fg` | `Token.AccentForegroundColor` | `Token.AccentForegroundBrush` |
| `--bg-canvas` | `Token.BgCanvasColor` | `Token.BgCanvasBrush` |
| `--bg-surface` | `Token.BgSurfaceColor` | `Token.BgSurfaceBrush` |
| `--bg-subtle` | `Token.BgSubtleColor` | `Token.BgSubtleBrush` |
| `--bg-muted` | `Token.BgMutedColor` | `Token.BgMutedBrush` |
| `--bg-inverse` | `Token.BgInverseColor` | `Token.BgInverseBrush` |
| `--text-primary` | `Token.TextPrimaryColor` | `Token.TextPrimaryBrush` |
| `--text-secondary` | `Token.TextSecondaryColor` | `Token.TextSecondaryBrush` |
| `--text-tertiary` | `Token.TextTertiaryColor` | `Token.TextTertiaryBrush` |
| `--text-disabled` | `Token.TextDisabledColor` | `Token.TextDisabledBrush` |
| `--text-inverse` | `Token.TextInverseColor` | `Token.TextInverseBrush` |
| `--text-accent` | `Token.TextAccentColor` | `Token.TextAccentBrush` |
| `--border-subtle` | `Token.BorderHairColor` (rename → `Token.BorderSubtleColor`) | `Token.BorderSubtleBrush` |
| `--border-default` | `Token.BorderSoftColor` (rename → `Token.BorderDefaultColor`) | `Token.BorderDefaultBrush` |
| `--border-strong` | `Token.BorderStrongColor` | `Token.BorderStrongBrush` |
| `--border-accent` | `Token.BorderAccentColor` | `Token.BorderAccentBrush` |
| `--status-success-*` | `Token.StatusOk*Color` (rename → `Token.StatusSuccess*Color`) | `Token.StatusSuccess*Brush` |
| `--status-warning-*` | `Token.StatusWarn*Color` (rename → `Token.StatusWarning*Color`) | `Token.StatusWarning*Brush` |
| `--status-danger-*` | `Token.StatusBad*Color` (rename → `Token.StatusDanger*Color`) | `Token.StatusDanger*Brush` |
| `--status-info-*` | *(add — currently missing)* | |

**MyList-specific (non-Forma) tokens — keep as-is, prefix `Token.MyList.*`:**

| Mylist CSS | WPF key |
|---|---|
| `--mylist-bg-mica` | `Token.BgMicaColor` → rename `Token.MyList.BgMicaColor` |
| `--mylist-bg-app` | *(add)* `Token.MyList.BgAppColor` |
| `--mylist-bg-hover` | `Token.BgHoverColor` → rename `Token.MyList.BgHoverColor` |
| `--mylist-bg-active` | `Token.BgActiveColor` → rename `Token.MyList.BgActiveColor` |
| `--mylist-h-ok / -warn / -bad / -unk` | `Token.MyList.Health{Ok,Warn,Bad,Unknown}Color` |
| `--mylist-t-{file,folder,mtab,clip,action}-{bg,fg,bd}` | `Token.MyList.Type{Slot}{Role}Color` (already exists as `Token.Type*` — just add `MyList.` prefix or leave) |
| `--mylist-shadow-focus` | `Token.ShadowFocusBrush` |

> **Action:** do the renames in one PR with mechanical find/replace. After this, every WPF brush has a 1:1 Forma counterpart, and you can name the new tokens directly after the Forma vars going forward (e.g. anyone reading the prototype CSS can grep the WPF resource dictionary).

> **Shipped (PR #9, #10):** `Token.MyList.Health{Ok,Bad,Unknown}Brush` and `Token.MyList.Type{File,Folder,Mtab,Clip,Action}Brush` are SolidColorBrush **aliases** over the existing `Token.Status*Color` and `Token.Type*BackgroundColor` definitions — not new colors. Themes cascade automatically via `DynamicResource Color` references. Foreground for type chips uses the matching saturated `Token.TypeXxxForegroundBrush` for WCAG-AA contrast on pastel backgrounds.

### 1.2 Typography

| Forma | WPF |
|---|---|
| `--font-sans` (Plus Jakarta Sans) | `Token.FontSans` — `pack://application:,,,/Assets/Fonts/#Plus Jakarta Sans` |
| `--font-mono` (JetBrains Mono) | `Token.FontMono` — `pack://application:,,,/Assets/Fonts/#JetBrains Mono` |
| `--text-xs` 11px | `Token.TextXs = 11` |
| `--text-sm` 12px | `Token.TextSm = 12` |
| `--text-md` 13px | `Token.TextMd = 13` |
| `--text-base` 14px | `Token.TextBase = 14` |
| `--text-lg` 16px | `Token.TextLg = 16` |
| `--text-xl` 18px | `Token.TextXl = 18` |
| `--text-2xl` 22px | `Token.Text2Xl = 22` |
| `--text-3xl` 28px | `Token.Text3Xl = 28` |
| `--weight-medium` 500 | `Token.WeightMedium = Medium` |
| `--weight-semibold` 600 | `Token.WeightSemibold = SemiBold` |
| `--weight-bold` 700 | `Token.WeightBold = Bold` |

> **Action:** add `Token.Text*` doubles and `Token.Weight*` `FontWeight` keys to `Tokens.xaml`. Today these are inlined in view XAML.

### 1.3 Radius

| Forma | WPF (already correct) |
|---|---|
| `--radius-xs` 2 | `Token.RadiusXs = 3` ⚠ off-by-one — fix to `2` |
| `--radius-sm` 4 | `Token.RadiusSm = 4` ✓ |
| `--radius-md` 6 | `Token.RadiusMd = 6` ✓ |
| `--radius-lg` 8 | `Token.RadiusLg = 8` ✓ |
| `--radius-xl` 12 | `Token.RadiusXl = 10` ⚠ — fix to `12` |
| `--radius-2xl` 16 | `Token.Radius2Xl = 14` ⚠ — fix to `16` |
| `--radius-3xl` 24 | `Token.Radius3Xl = 20` ⚠ — fix to `24` |
| `--radius-full` 9999 | `Token.RadiusFull = 9999` ✓ |

### 1.4 Spacing — keep the existing 0/1/2/3/4/5/6/8/10/12… ladder; it already aligns with Forma's 4px grid.

### 1.5 Shadow

Forma uses `rgba(15,15,10,X)` warm-tinted shadows in light, opaque-black in dark. WPF shadows today are opaque black for both. Add:

```xml
<!-- light theme dictionary -->
<DropShadowEffect x:Key="Token.ShadowMd" BlurRadius="12" ShadowDepth="3"
                  Color="#0F0F0A" Opacity="0.08" />
<!-- dark theme dictionary keeps current opaque-black values -->
```

### 1.6 Motion

| Forma | WPF |
|---|---|
| `--duration-fast` 100ms | `Token.DurationFast = 0:0:0.10` |
| `--duration-normal` 200ms | `Token.DurationNormal = 0:0:0.20` |
| `--duration-slow` 300ms | `Token.DurationSlow = 0:0:0.30` |
| `--ease-standard` 0.4,0,0.2,1 | `Token.EaseStandard` ✓ |
| `--ease-decelerate` 0,0,0.2,1 | `Token.EaseDecel` → rename `Token.EaseDecelerate` |
| `--ease-spring` 0.34,1.56,0.64,1 | `Token.EaseSpring` ✓ (currently `0.34,1 0.64,1` — fix the second control point to `1.56` to match Forma's spring) |

---

## 2. Theme dictionary layout

Replace the current `Resources/Colors.{Dark,Light}.xaml` pair with a 16-dictionary matrix matching Forma:

```
Resources/Colors.Dark.Amber.xaml
Resources/Colors.Dark.Blue.xaml          ← MyList default
Resources/Colors.Dark.Green.xaml
Resources/Colors.Dark.Violet.xaml
Resources/Colors.Dark.Teal.xaml
Resources/Colors.Dark.Terracotta.xaml
Resources/Colors.Dark.Crimson.xaml
Resources/Colors.Dark.Slate.xaml
Resources/Colors.Light.{same 8}.xaml
```

Each dictionary overrides **only the semantic layer** (`Token.AccentColor`, `Token.BgSurfaceColor`, `Token.TextPrimaryColor`, etc.) — primitives and structural tokens stay in `Tokens.xaml`.

`ThemeService.ApplyTheme(string theme, string accent)` becomes a tuple call:

```csharp
public void ApplyTheme(ThemeMode mode, AccentPalette accent) {
    var key = $"Colors.{mode}.{accent}.xaml";   // e.g. "Colors.Dark.Blue.xaml"
    var uri = new Uri($"Resources/{key}", UriKind.Relative);
    // replace whichever Colors.*.*.xaml is currently in Application.Resources.MergedDictionaries
}
```

Persist `(Mode, Accent)` as two `AppSettings` properties; existing `Theme` enum becomes `ThemeMode` + new `AccentPalette` enum.

---

## 3. Constellation surfaces — XAML patterns

These are the features the Simple variant (currently merged) does **not** have. Each needs a real WPF implementation.

### 3.1 Kbd chip — per item row, ⌘1–⌘9

Hidden at rest, visible on hover/selection. Drop into `ListItemTemplate` in `MainWindow.xaml`:

```xml
<Border x:Name="KbdChip"
        Margin="0,0,8,0"
        Padding="6,1"
        Background="{DynamicResource Token.BgSubtleBrush}"
        BorderBrush="{DynamicResource Token.BorderSubtleBrush}"
        BorderThickness="1"
        CornerRadius="{StaticResource Token.RadiusXs}"
        Opacity="0">
  <TextBlock FontFamily="{DynamicResource Token.FontMono}"
             FontSize="{StaticResource Token.TextXs}"
             Foreground="{DynamicResource Token.TextSecondaryBrush}">
    <Run Text="⌘"/><Run Text="{Binding RelativeSource={RelativeSource AncestorType=ListBoxItem}, Path=(helpers:ItemIndex.Value)}"/>
  </TextBlock>
</Border>
<!-- triggers via ListBoxItem -->
<DataTrigger Binding="{Binding RelativeSource={RelativeSource AncestorType=ListBoxItem}, Path=IsMouseOver}" Value="True">
  <Setter TargetName="KbdChip" Property="Opacity" Value="1"/>
</DataTrigger>
```

Use the existing `helpers:ItemIndex` attached property (or `AlternationIndex` + `+1` converter). Bind to first 9 only — items 10+ get no chip.

> **Shipped (PR #5):** `ListBox.AlternationCount="9999"` (not `9`). WPF cycles AlternationIndex modulo AlternationCount; setting it to 9 makes row 10 reuse the chip text for row 1. `IntPlusOneConverter` is dual-mode — returns the `1..9` label string AND a `Visibility` value when target type is `Visibility`, hiding the chip on rows ≥ 10. PR #10 lifted the chip chrome to `Themes/Chips.xaml` as `Chip.Kbd` (ContentControl style); the row template keeps the fade animation + AlternationIndex visibility binding caller-side.

### 3.2 Sparkline — item row trajectory

Forma version is `polyline points="0,12 10,8 …"` in 60×16 SVG, stroke = `--accent`. WPF:

```xml
<Polyline Points="{Binding TrajectoryPoints}"
          Stroke="{DynamicResource Token.AccentBrush}"
          StrokeThickness="1.5"
          StrokeLineJoin="Round"
          StrokeStartLineCap="Round"
          StrokeEndLineCap="Round"
          Width="60" Height="16"
          Opacity="0.7"/>
```

`TrajectoryPoints` is a `PointCollection` computed in `ItemViewModel` from the last 14 access timestamps:

```csharp
public PointCollection TrajectoryPoints { get; private set; }

private void RecomputeTrajectory() {
    var buckets = HistoryService.LastN(14, Id);   // int[] of access counts
    var max = Math.Max(buckets.Max(), 1);
    var points = new PointCollection();
    for (int i = 0; i < buckets.Length; i++) {
        double x = i * (60.0 / (buckets.Length - 1));
        double y = 14 - (buckets[i] / (double)max) * 12;
        points.Add(new Point(x, y));
    }
    TrajectoryPoints = points;
    OnPropertyChanged(nameof(TrajectoryPoints));
}
```

### 3.3 Health ring — overlaid on `IconChrome`

Two `Ellipse`s stacked behind the icon glyph. Outer is the track, inner is the arc, both `StrokeDashArray`-clipped:

```xml
<Grid Width="24" Height="24">
  <!-- track -->
  <Ellipse Width="22" Height="22"
           Stroke="{DynamicResource Token.MyList.BgHoverBrush}"
           StrokeThickness="1.5"/>
  <!-- progress arc — RenderTransform rotates dash start to 12 o'clock -->
  <Ellipse Width="22" Height="22"
           Stroke="{Binding HealthBrush}"
           StrokeThickness="1.5"
           StrokeDashArray="{Binding HealthDashArray}"
           RenderTransformOrigin="0.5,0.5">
    <Ellipse.RenderTransform>
      <RotateTransform Angle="-90"/>
    </Ellipse.RenderTransform>
  </Ellipse>
  <!-- icon glyph centered -->
  <Path Data="{Binding IconPath}" Fill="{Binding IconBrush}"
        Width="12" Height="12" Stretch="Uniform"/>
</Grid>
```

`HealthDashArray` converter: `circumference = π × 22 ≈ 69.1`; for `HealthScore` ∈ [0,1] emit `$"{score*69.1:F2},{69.1:F2}"`. `HealthBrush` = success / warning / danger Brush per band (≥0.7 / ≥0.4 / else).

### 3.4 Trajectory bars — preview pane

Forma:  20 vertical bars, height ∝ access count. WPF:

```xml
<ItemsControl ItemsSource="{Binding PreviewItem.TrajectoryBars}">
  <ItemsControl.ItemsPanel>
    <ItemsPanelTemplate>
      <StackPanel Orientation="Horizontal"
                  HorizontalAlignment="Stretch"
                  VerticalAlignment="Bottom"/>
    </ItemsPanelTemplate>
  </ItemsControl.ItemsPanel>
  <ItemsControl.ItemTemplate>
    <DataTemplate>
      <Rectangle Width="6" Margin="1,0"
                 Height="{Binding Height}"
                 RadiusX="2" RadiusY="2"
                 Fill="{DynamicResource Token.AccentBrush}"
                 Opacity="{Binding Opacity}"/>
    </DataTemplate>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

`TrajectoryBars` is `IReadOnlyList<TrajectoryBar>` where `Height` is 4..48 and `Opacity` is 0.3..1 by recency.

> **Shipped (PR #6, #8, #9):** `TrajectoryPoints` uses a 14-day window (row sparkline); `TrajectoryBars` uses a 20-day window (preview pane). Both share the same private `ComputeDailyBuckets(days)` on `ItemModel` and invalidate together via `RaiseTrajectoryChanged()`. `TrendDelta` (PR #9 Constellation Trending) consumes a cached 14-day bucket array via `GetLast14DayBuckets()` — computes `last7 − prior7` without re-walking `OpenedHistory`. Adding new windows: extend the cache by adding a sibling field next to `_last14DayBucketsCache`; clear it in `RaiseTrajectoryChanged`.

### 3.5 Mini-launcher orbit (tray popup)

Constellation tray shows a 280×280 panel with the active item at center and 5–8 favorites orbiting on a circle. The existing `InlineMtabPathViewModel.UpdateLayout` already does polar math — generalize it into a `OrbitLayoutService`:

```csharp
public static IReadOnlyList<(double X, double Y)> Compute(int count, double cx, double cy, double rx, double ry) {
    var result = new List<(double, double)>(count);
    for (int i = 0; i < count; i++) {
        double θ = -Math.PI / 2 + 2 * Math.PI * i / count;
        result.Add((cx + rx * Math.Cos(θ), cy + ry * Math.Sin(θ)));
    }
    return result;
}
```

Reuse for: mtab cluster preview, tray orbit, and any future "constellation"-style picker.

---

## 4. Surface-by-surface diff vs current main

For each surface, what needs to change to reach Constellation parity.

| Surface | Status today (Simple variant on main) | Constellation work |
|---|---|---|
| **MainWindow** | Title-bar search, sidebar nav, item list, preview pane all present | Add kbd chips (3.1), sparklines (3.2), health rings (3.3) to `ListItemTemplate`; replace mtab cluster placeholder with real polar viz (already done in `0085f7d` per the follow-up review). |
| **Sidebar** | Sections + nav items wired correctly | Add a "Constellation" group at the bottom: Pinned / Recent / Trending. Use `Token.MyList.Health*` colors for the trending arrow chips. |
| **Preview pane** | Item metadata, action buttons | Add `Trajectory` block (3.4) above metadata; add Sparkline of last-7-day access pattern. |
| **Command palette** | Modal opens via `Ctrl+K`, filters items | Add per-row icon (already in sprite), keyboard chip chip, type badge. Group results by section: "Items · Commands · Settings". |
| **Item editor** | Side overlay drawer | Replace `Windows Terminal` overflow with `Terminal` (done in prototype, port to XAML — `ActionItemWindow.xaml:90`). Add runner segmented control using `Forma`-style toggle buttons. |
| **Settings** | View-swap (not modal) | Ensure all toggles use `Simple.Toggle` style; fix Appearance section toggle cropping (user-reported). Add Accent picker (8 swatches × 2 modes). |
| **Clipboard / Duplicates** | UserControls overlay the items pane | Add type badges per row using `Token.MyList.Type*` colors. **Reality (PR #12):** clipboard rows are `IsHitTestVisible="False"` display-only — no per-row triage/dismiss/edit/copy actions exist; no leading kind glyph (only colored status ellipse); no empty-state glyph. Duplicate rows use `<Image Source="{Binding Icon}">` system icons, not inline `<Path>`. Only the type-chip swap shipped; spec's fictional per-row affordances + group-header chrome were dropped. Top-level duplicate buttons (Rescan/Merge selected/Merge all/Back) got Lucide leading glyphs as drive-by polish. |
| **Tray / mini-launcher** | Not implemented | Build per 3.5. New `Views/MiniLauncherWindow.xaml`, owner-less window, summons on `Ctrl+Alt+Space` (already a hotkey, currently a no-op). |
| **Theming** | Light + dark, blue accent only | Generate 16 dictionaries per §2. Wire `AccentPalette` enum + Settings picker. |

---

## 5. Direct mapping of user-reported issues

> *"some icons not working right"* — fixed in prototype (icon sprite was an empty placeholder). For WPF this never applied; icons there are XAML `Path` data. Verify the `Path Data` strings in `MainWindow.xaml`'s `ListItemTemplate` against the sprite in `assets/icons.svg`. Any mismatches mean a stale XAML path that needs re-tracing from the SVG.
> **Fixed (PR #9 / #11 / #13):** `MahApps.Metro.IconPacks.Lucide` referenced; all functional inline `<Path Data>` icons swapped to `PackIconLucide Kind=…` across `MainWindow.xaml` + sidebars + clipboard + duplicates + palette + segmented control. 11 dead `Icon.*` `<Geometry>` resources removed. Only one `<Path Data>` survives: the MyList brand badge in the sidebar footer (line 385) — intentional, no Lucide equivalent.

> *"some setting toggles are cropped"* — `SettingsView.xaml` toggle row needs `Grid.ColumnDefinitions` with `*` for label, `Auto` for control, plus `ClipToBounds="False"`. Audit each `<RowDefinition Height="Auto">` for fixed widths that clip the toggle thumb at small window widths.
> **Status (PR #13 Part A):** could not reproduce against current build. `SettingsView.xaml` has zero `ClipToBounds` ancestors. Toggle template is hard-bounded at 36×20; row chrome is `<Border><Grid ColumnDefs="*, Auto">`. If repro found, suspect Compact density compressing row height below `20 + 2px focus-ring margin`. Repro steps required from reporter to close.

> *"some text are big on the button"* — currently every button uses `FontSize="14"` inlined in XAML. After §1.2 lands, `Simple.Button` style sets `FontSize="{DynamicResource Token.TextSm}"` (12 px) for utility buttons, `Token.TextBase` (14) for primary CTAs. Audit `SimpleControls.xaml` line ~16–80 — there are 3 styles using `TextBase` that should use `TextSm`.
> **Status (PR #13 Part B):** no utility-button `FontSize=TextBase` overrides found in `Views/`. `Simple.Button` base is already `TextSm` (12); only `Simple.PrimaryButton` overrides to `TextBase` (intentional). One `TextBase` hit in `MainWindow.xaml:1029` is the "Nothing here yet" empty-state heading TextBlock, not a Button. No targets — closed as "no-repro on buttons".

---

## 6. Suggested PR sequence

1. **Token rename + Forma alignment** *(small, mechanical)*
   - Rename `Token.BorderHair* → Token.BorderSubtle*`, `Token.BorderSoft* → Token.BorderDefault*`, `Token.StatusOk* → Token.StatusSuccess*`, `Token.StatusBad* → Token.StatusDanger*`, `Token.AccentSoft* → Token.AccentSubtle*`, `Token.AccentMute* → Token.AccentMuted*`, `Token.EaseDecel → Token.EaseDecelerate`.
   - Fix radius mismatches (xs 3→2, xl 10→12, 2xl 14→16, 3xl 20→24).
   - Fix spring easing control point (0.34,1 → 0.34,1.56).
   - Add `Token.Text*` doubles, `Token.Weight*` `FontWeight`s, `Token.FontSans`, `Token.FontMono`.

2. **Theme matrix** *(medium)*
   - Generate 16 `Colors.{Mode}.{Accent}.xaml` from `forma/themes.css` — they're literally a token-by-token transcription.
   - Refactor `ThemeService` per §2. Add `AccentPalette` enum + Settings picker UI.
   - Bundle Plus Jakarta Sans + JetBrains Mono `.ttf` in `Assets/Fonts/`. Update `csproj` `<Resource>` items.

3. **Constellation features** *(per-surface PRs)*
   - 3.1 Kbd chips (smallest)
   - 3.4 Trajectory bars in preview
   - 3.2 Sparkline in item rows
   - 3.3 Health ring on icon chrome
   - 3.5 Mini-launcher orbit

4. **Reported bugs** — fold into the relevant feature PR or one cleanup PR (§5).

5. **Tests** — finally add `MyList.Tests` xUnit project covering `OrbitLayoutService`, `ThemeService.ApplyTheme(mode, accent)`, `ItemViewModel.RecomputeTrajectory`, plus existing `SearchQueryParser` and `PathNormalizationHelper`.

---

## Appendix A — what NOT to do

- ❌ Don't add brushes for combinations Forma covers semantically (e.g. don't create a `Token.AccentSubtleOnSurfaceBrush` — compose with opacity at the consumer site).
- ❌ Don't introduce a `Mica`-style brush in XAML; let Windows compose it via `DwmSetWindowAttribute` (see `ThemeService.cs:131–134`). Root window `Background` should be `Transparent` on Win11, fallback to `Token.MyList.BgMicaBrush` only when Mica isn't available.
- ❌ Don't reach for emoji or unicode glyphs anywhere — extend the sprite instead.
- ❌ Don't hand-pick a hex color for a "missing" state — every color comes from `Token.*`. If a color doesn't exist, that's a token-system gap to discuss first.

---

## Appendix B — files touched

```
Mylist/Themes/Tokens.xaml                          (rename + add tokens)
Mylist/Resources/Colors.{Dark,Light}.xaml          (delete; replace with 16-file matrix)
Mylist/Resources/Colors.{Mode}.{Accent}.xaml × 16  (new)
Mylist/Services/ThemeService.cs                    (mode+accent tuple)
Mylist/Models/AppSettings.cs                       (split Theme into ThemeMode + AccentPalette)
Mylist/Assets/Fonts/PlusJakartaSans-*.ttf          (new)
Mylist/Assets/Fonts/JetBrainsMono-*.ttf            (new)
Mylist/Views/MainWindow.xaml                       (kbd chip, sparkline, health ring, trajectory)
Mylist/Views/SettingsView.xaml                     (accent picker, toggle clipping fix)
Mylist/Views/ActionItemWindow.xaml                 (runner segmented control, "Terminal" rename)
Mylist/Views/MiniLauncherWindow.xaml               (new)
Mylist/Services/OrbitLayoutService.cs              (new — extracted from InlineMtabPathViewModel)
Mylist/ViewModels/ItemViewModel.cs                 (TrajectoryPoints, HealthScore, HealthBrush)
Mylist/Helpers/PercentToDashArrayConverter.cs      (new — for health ring)
Mylist.Tests/                                      (new project)
```
