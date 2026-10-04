# 02 — Architecture

## Tech baseline

- **.NET 10** (current LTS), C# latest, nullable enabled, `TreatWarningsAsErrors`.
- **Terminal.Gui 2.5.x** for the UI.
- **xUnit** for tests (or your existing preference).
- Developed in Rider or Cursor; runs from any terminal via `dotnet run --project src/Myc.App`.
- Shipped as a single Native AOT binary named `myc` (Homebrew tap after MVP1).

## Solution layout

```
src/
  Myc.sln
  Myc.Core/                # No UI dependencies. File system model + operations.
    FileSystem/            # FileEntry, DirectoryListing, IFileSystem (thin wrapper)
    Operations/            # Copy, Move, Delete/Trash, Rename, CreateDirectory jobs
    Selection/             # Mark set, "effective selection" rules
    Sorting/               # Natural, case-insensitive comparers; dirs-first
    Platform/              # macOS specifics: Trash via Foundation, `open`, xattrs
  Myc.App/                 # Terminal.Gui front-end; assembly/binary name `myc`
    Views/                 # FilePanelView, FunctionKeyBar, PanelHeader, StatusLine
    Dialogs/               # Confirm, Copy/Move, Rename, MkDir, Progress, Conflict
    Commands/              # Command ids + key bindings (single source of truth)
    Theming/               # Built-in themes, theme loader
    Program.cs
tests/
  Myc.Core.Tests/          # Real temp directories; no mocks needed for most cases
  Myc.App.Tests/           # Headless Terminal.Gui driver tests for key flows
spike/                     # Throwaway framework spike (deleted after decision)
```

## Core concepts

### `FileEntry`
An immutable record: name, full path, kind (file / directory / symlink / other), size, modified
time, hidden flag, permissions, and symlink target. The listing always includes a synthetic `..`
entry first, except at `/`.

### `PanelState` (per panel)
- `CurrentDirectory`
- `Entries` (sorted listing)
- `CursorIndex` and scroll offset
- `Marks`: a set of entry names (not indices, so they survive refresh and re-sorting)
- `SortMode`, `ShowHidden`

### Effective selection (Norton rule)
> If any items are marked, operations act on the **marked items**. If nothing is marked, they
> act on the **item under the cursor** (never on `..`).

This one rule drives F5, F6, F8 and the F7 "move into new folder" option, and it lives in
`Myc.Core.Selection` so it is tested once.

### Operations as jobs
Copy, Move and Delete run as `IFileJob` instances:

- They run on a background `Task` with a `CancellationToken`.
- They report `IProgress<JobProgress>` (current item, bytes done / total, items done / total).
- Conflicts and errors are raised as **questions** (`Overwrite / Skip / Rename / Overwrite all /
  Skip all / Cancel`, or `Retry / Skip / Cancel` for errors). The UI answers via an async
  callback, so the core never knows about dialogs.
- They pre-scan first (count items and bytes) so progress is meaningful.

Rename and CreateDirectory are synchronous one-shots.

### Threading
Terminal.Gui has a UI main loop. Jobs report progress from a background thread and marshal UI
updates through the app's invoke mechanism (`IApplication.Invoke` or equivalent). The rule:
**no view is touched off the UI thread.**

### Refresh
- MVP0: after any operation, refresh both panels; `Ctrl+R` forces a refresh.
- MVP1+: a `FileSystemWatcher` per panel (FSEvents-backed on macOS), debounced at about 250 ms.

### Platform layer (macOS first, portable shape)
- **Trash:** `NSFileManager trashItemAtURL:` via a small Objective-C runtime P/Invoke shim
  (`objc_msgSend`). This avoids shelling out and keeps "Put Back" working in Finder.
- **Open with default app:** `open <path>`.
- Abstracted behind `IPlatformServices`, so Linux (`gio trash`, `xdg-open`) can be added later
  without touching the core.

## Key bindings as data
All commands have an id (`Panel.CursorDown`, `File.Copy`, ...). One table maps keys to ids and
feeds three things: input handling, the function-key bar labels, and the F1 help screen. That
table is what the MVP1 menu and future user key remapping build on.

## Testing strategy
- **Core:** create real temp directory trees per test, run operations, assert on the resulting
  tree. Cover conflicts, cancellation mid-copy, symlinks, permission errors, copying a folder
  into itself (must be refused), and same-source/same-target copies.
- **App:** headless Terminal.Gui tests for the key flows: Space marks and moves down, Left/Right
  navigation restores the cursor, F7 with marks moves the items, F8 confirmation cancels.
- **Manual matrix:** Ghostty, iTerm2, WezTerm, Terminal.app, plus 80×24 and very wide windows.

## Edge cases to design for early
- Permission-denied directories: show an inline error in the panel instead of crashing or
  navigating.
- Symlinks: show with an indicator (`→`); copy links as links, and never follow them recursively
  during copy or delete.
- Very large directories: virtualized drawing, sorting off the UI thread if needed.
- Long names: middle-ellipsis (`very-long-fi…name.txt`) so the extension stays visible.
- Directory removed or unmounted while displayed: fall back to the nearest existing parent.
