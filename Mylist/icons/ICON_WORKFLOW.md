# MyList Icon Workflow

This folder contains the runtime `.ico` files used by MyList:

- `clipboard.ico`
- `cmd.ico`
- `folder.ico`
- `mtab.ico`
- `powershell.ico`

## Source Structure

Use this production layout for each icon:

- `source/<icon-name>/master/`
  - Keep editable source files here (`.svg`, `.afdesign`, `.psd`, `.fig`, etc.)
- `source/<icon-name>/png/`
  - Export one PNG per target size here before packaging into `.ico`
- `source/<icon-name>/notes.md`
  - Optional notes for simplification, palette, and QA

Recommended target sizes:

- `16x16`
- `20x20`
- `24x24`
- `32x32`
- `40x40`
- `48x48`
- `64x64`
- `128x128`
- `256x256`

## File Naming

Inside each `png/` folder, export files using this pattern:

- `<icon-name>-16.png`
- `<icon-name>-20.png`
- `<icon-name>-24.png`
- `<icon-name>-32.png`
- `<icon-name>-40.png`
- `<icon-name>-48.png`
- `<icon-name>-64.png`
- `<icon-name>-128.png`
- `<icon-name>-256.png`

Examples:

- `folder-16.png`
- `folder-20.png`
- `folder-24.png`
- `folder-32.png`
- `folder-40.png`
- `folder-48.png`
- `folder-64.png`
- `folder-128.png`
- `folder-256.png`

## Small-Size Simplification Rules

### `16x16`
- Treat as a glyph
- Keep only the core silhouette
- No fine inner details

### `20x20`
- Very simple
- One primary shape, one secondary cue at most

### `24x24`
- Still simplified
- Strong contrast edges
- Avoid blur/glow

### `32x32`
- Main working size for MyList list/tile rendering
- Hand-tune this size
- Keep one clear focal detail only

### `40x40` and up
- More detail is acceptable
- Keep the same silhouette language as the smaller sizes

## Packaging Back Into `.ico`

1. Start from master artwork at `512x512` or `1024x1024`
2. Export all target PNG sizes into `source/<icon-name>/png/`
3. Hand-tune at least:
   - `16x16`
   - `20x20`
   - `24x24`
   - `32x32`
4. Package all PNGs into a real Windows `.ico`
5. Make sure the `256x256` entry is PNG-compressed inside the `.ico`
6. Replace the runtime file in `MyList/icons/`

## Runtime Mapping

MyList uses these icon names directly:

- `mtab.ico` -> Mtab items
- `powershell.ico` -> PowerShell action items
- `cmd.ico` -> Command and Batch action items
- `clipboard.ico` -> clipboard items and clipboard-image fallback
- `folder.ico` -> custom folder fallback only

Normal folders and files should prefer Windows shell icons first. The runtime `folder.ico` is only a fallback.

## QA Checklist

Before replacing any runtime icon:

1. Check readability on dark background
2. Check readability on light background
3. Verify `16x16`, `20x20`, `24x24`, and `32x32` manually
4. Confirm no muddy gradients at small sizes
5. Confirm the icon is still recognizable in one glance
