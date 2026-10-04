# 05 — High-level roadmap (after MVP1)

Ordered by value; each milestone is meant to be shippable on its own.

## MVP2 — Theme selection

- **Options → Theme…** opens a picker with a **live preview** (the panels re-theme as you move
  through the list; `Esc` reverts, `Enter` keeps it).
- **Built-in themes:**
  - **Graphite**: the default dark theme with a teal accent.
  - **Paper**: a light theme for light terminal profiles.
  - **Terminal palette**: uses only the 16 ANSI colours, so it automatically matches whatever
    theme your terminal has (the most "native" option).
  - **High contrast**: for accessibility and bright environments.
  - Optionally a **Retro** theme, a respectful nod to NC with muted colours, never the default.
- **User themes:** JSON files in `~/.config/myc/themes/`, using the same role names as the
  built-ins (background, text, directory, cursor, marked, accent, danger, border-active,
  border-inactive, and so on). Hot-reloaded when the file changes.
- **Auto light/dark:** optionally follow macOS appearance by mapping to a light/dark theme pair.

## MVP3 — Panel modes

Each panel can independently switch mode (Panel menu, plus a shortcut such as `Ctrl+1/2/3`).
A common pattern: list on the left, preview on the right, following the left panel's cursor.

1. **File list** (default)
   - *Full*: name, size, modified.
   - *Brief*: names only, in multiple columns (good for big directories).
2. **Quick preview** (of the item under the cursor in the *other* panel)
   - Text and code: first N KB, with optional lightweight syntax highlighting.
   - Images: inline rendering through the Kitty / iTerm2 graphics protocols or Sixel where the
     terminal supports it, falling back to block characters. Shares ideas with Viewism.
   - Markdown: rendered with Terminal.Gui's Markdown view.
   - Directories: a summary of their contents.
   - Binary: hex dump of the header and the detected file type.
3. **File / directory info**
   - Size (and, for folders, a cancellable on-demand recursive size calculation).
   - Created / modified / accessed dates, permissions (`rwx` and octal), owner and group.
   - Symlink target, item counts for folders.
   - macOS extras: Finder tags, quarantine flag (`com.apple.quarantine`), and "Where from"
     (download URL) from extended attributes.

## Later candidates (to pick from, not committed)

| Area | Ideas |
| --- | --- |
| View / edit | F3 built-in viewer (text, hex), F4 opens `$EDITOR` and returns cleanly |
| Navigation | Type-to-jump / quick filter, bookmarks / hotlist (`Ctrl+B`), directory history (`Alt+←/→`), tabs per panel |
| Search | Find files by name or content (`Alt+F7`), results as a virtual panel |
| Bulk ops | Multi-rename with pattern and preview, compare / sync directories |
| macOS integration | Quick Look (`qlmanage -p`), "Open terminal here", drag-out to Finder (where supported) |
| Archives | Browse `.zip` / `.tar.gz` as folders; create archives from the selection |
| Remote | SFTP panels (SSH.NET), later S3 or WebDAV |
| Job queue | Background copy queue with a jobs panel; keep working while large copies run |
| Key remapping | User key bindings in JSON, built on the existing command table |
| Platforms | Linux support (trash via `gio`, `xdg-open`); Windows Terminal later |
| Distribution | Homebrew tap with a Native AOT binary; `dotnet tool` package |

## Non-goals

- Becoming Midnight Commander: no built-in shell, no user-menu scripting language, no FTP-era
  baggage.
- Drawing a fake desktop GUI inside the terminal. It should look like a well-made terminal app,
  not a window manager.
