# 05 — High-level roadmap (after MVP1)

Ordered by value; each milestone is meant to be shippable on its own.

## MVP2 — User themes

MVP1 already ships Options → Theme… for Graphite, Paper, and Terminal.Gui's built-in themes.
What remains:

- **Live preview:** the panels re-theme as you move through the list; `Esc` reverts, `Enter`
  keeps it.
- **More myc themes:** a terminal-palette theme (the 16 ANSI colours, so it follows the
  terminal), a high-contrast theme, and optionally a muted Retro theme. Retro is never the
  default.
- **User themes:** JSON files in `~/.config/myc/themes/`, using the same role names as the
  built-ins (background, text, directory, cursor, marked, accent, danger, border-active,
  border-inactive, and so on). Hot-reloaded when the file changes. `SwitchTheme` will not see
  these names; they load the same way Graphite and Paper do.
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

## MVP4 — Terminal panel (proposed)

A panel mode that hosts a real terminal, so a shell, Claude Code, or Codex runs beside the
file list. It also adds a full-screen shell hand-off and a way to send marked files to the
agent's prompt. Research, risks, and open decisions are in [06-mvp4.md](06-mvp4.md). It is
not approved yet and is gated by a spike.

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
  baggage. MVP4 proposes narrowing "no built-in shell" to "no command line under the panels
  and no subshell tricks", so that a panel may host a real terminal. See
  [06-mvp4.md](06-mvp4.md#open-decisions).
- Drawing a fake desktop GUI inside the terminal. It should look like a well-made terminal app,
  not a window manager.
