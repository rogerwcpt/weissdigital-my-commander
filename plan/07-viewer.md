# 07 — Viewer

**Status:** planned. It does not depend on themes (MVP2), panel modes (MVP3), or the terminal
panel (MVP4), so it can be built as soon as it is asked for.

**Goal:** F3 opens the file under the cursor in a full-screen viewer and returns to the same
place. Plain text is the base. Markdown files render with the view Terminal.Gui already ships.

F3 is reserved today. `MycCommand.View` is in the command table with the label `—` and the
summary `Reserved`, so the function bar already has a dimmed slot for it.

## Feasibility

Markdown rendering does not need a new library. Terminal.Gui 2.5, the version `Myc.App` already
references, includes a read-only `Markdown` view built on Markdig's advanced pipeline. Setting
its text parses the document, word-wraps it, and scrolls.

That view can show:

- Headings, paragraphs, and emphasis
- Lists
- Links. An anchor (`#heading`) scrolls inside the view. Following any other link can wait;
  the first version leaves those as text.
- Fenced code
- Pipe tables
- Horizontal rules

Limits, so the viewer does not pretend to be a browser:

- Images stay as their alt text. Drawing the picture belongs with the graphics-protocol work
  already sketched for panel preview.
- Raw HTML is stripped to text.
- Math, Mermaid, and similar fences stay as code, not diagrams.
- The whole file is parsed in memory. Cap a view at 1 MiB, which is enough for notes, and say
  so when a file is larger.
- A NUL in the first 8 KB means the file is not text. Do not try Markdown on it. A hex view
  stays a later idea.

The main risk is colour. The Markdown view has its own style roles and may ignore Graphite,
Paper, and `NO_COLOR`. The first implementation must follow the active theme. If those roles
cannot be mapped, render that file as plain text styling instead of leaving a second palette
on screen.

MVP3's quick preview uses this same view for Markdown. Preview and F3 do not grow two
renderers.

## What F3 does

- F3 on a file opens a full-screen runnable over the panels. F3 or Esc closes it and returns
  to the same cursor. F10 still quits myc. A directory or `..` does not open the viewer.
- The File menu gains View, with F3 shown beside it.
- Plain text is the base. Up, Down, Page Up, Page Down, Home, and End scroll. `W` toggles
  wrap.
- `.md`, `.markdown`, and `.mdown` that pass the text check start rendered. `M` switches
  between rendered and source. Any other text file starts as source; `M` can still try
  Markdown on it.
- Reading happens off the UI thread. Only the view is touched on the UI thread, the same rule
  as the copy jobs.

## Manual check

In Ghostty, open a Markdown file that has a heading, a list, a table, a fenced block, and an
image. The image shows its alt text. Repeat with Graphite and with `NO_COLOR=1`. Esc and F3
both return to the panel, on the same file.
