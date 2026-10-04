# myc — a modern Norton Commander for the terminal

A dual-panel, keyboard-first file manager for the terminal, built in .NET. It keeps the
Norton Commander muscle memory (F-keys, two panels, mark-then-act) and drops the parts of
Midnight Commander that feel dated or opaque (dense menus, odd defaults, blue/yellow palette,
modal surprises).

> **Name:** `myc` (short for *my commander*) is both the product name and the command you
> type to launch it. The .NET projects use `Myc.*` (for example, `Myc.Core`), and config
> lives in `~/.config/myc/`.

## Guiding principles

1. **Norton muscle memory, modern manners.** F5 copy, F7 mkdir, F8 delete, Tab to switch
  panels — but with clear dialogs, sane defaults, and no hidden modes.
2. **A good terminal citizen.** Feels native in Ghostty / iTerm2 / WezTerm / Terminal.app:
  fast startup, respects the terminal font, honours `NO_COLOR`, restores the screen on exit,
   no fake "window chrome".
3. **Safe by default.** Delete goes to the macOS Trash unless explicitly told otherwise.
  Every destructive operation is confirmed and cancellable.
4. **State is never colour-only.** Marked items get a glyph as well as a colour, so the UI
  works with any theme and for colour-blind users.
5. **Core logic is UI-free.** File operations live in a plain .NET library with tests; the
  TUI is a thin shell on top. This keeps the door open for a future GUI front-end.



## Documents


| Doc                                      | What it covers                                                |
| ---------------------------------------- | ------------------------------------------------------------- |
| [01-framework.md](01-framework.md)       | TUI framework evaluation and recommendation (Terminal.Gui v2) |
| [02-architecture.md](02-architecture.md) | Solution layout, core abstractions, threading, testing        |
| [03-mvp0.md](03-mvp0.md)                 | MVP0: dual panels and the core F-key operations               |
| [04-mvp1.md](04-mvp1.md)                 | MVP1: F9 menu with clean, minimal functionality               |
| [05-roadmap.md](05-roadmap.md)           | Later work: themes, panel modes, viewer, and beyond           |
| [06-mvp4.md](06-mvp4.md)                 | MVP4 (proposed): a panel that hosts a shell or an AI CLI       |




## Milestones at a glance


| Milestone | Scope                                                                                                |
| --------- | ---------------------------------------------------------------------------------------------------- |
| **Spike** | Prove Terminal.Gui v2 on macOS terminals: F-keys, custom panel view, theming, AOT                    |
| **MVP0**  | Dual panels, navigation, Space marking, F2/F5/F6/F7/F8, Tab, F10 quit, default non-NC theme          |
| **MVP1**  | F9 menu bar (File / Select / Panel / Options), sort, hidden files, go-to-path, help, persisted state, theme selector |
| **MVP2**  | User JSON themes, live preview, more myc themes, auto light/dark                                     |
| **MVP3**  | Panel modes: file list, quick preview, file/directory info                                           |
| **MVP4**  | *Proposed.* Terminal panel for a shell or AI CLI, shell hand-off, send marked files to the prompt    |
| **Later** | F3 viewer, F4 edit via `$EDITOR`, quick filter, bookmarks, search, archives, Finder integration      |




## Decisions

Approved on 4 Oct 2026.

1. **F6 = Move.** Moves the effective selection to the other panel (copy, then remove from
   the source), as the partner of F5 Copy. Clearing all marks is `Esc` in a panel that has
   marks (also in MVP1's Select menu).
2. **F7 "Move selection into new folder?"** The checkbox appears when **one or more** items
   are marked.
3. **Delete.** F8 moves to the macOS Trash (recoverable). `Shift+F8` deletes permanently,
   with a stronger confirmation.
4. **Name: `myc`.** Product name and command name. The repo stays `weissdigital-my-commander`.
5. **Distribution.** `dotnet run` during development; a Homebrew tap with a Native AOT `myc`
   binary once MVP1 is stable.

