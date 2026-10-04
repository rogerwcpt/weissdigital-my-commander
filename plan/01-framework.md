# 01 — TUI framework choice

**Recommendation: [Terminal.Gui v2](https://github.com/gui-cs/Terminal.Gui)** (NuGet `Terminal.Gui`,
current stable **2.5.0**, MIT).

Spectre.Console is excluded per the brief: it is a rendering/prompt library, not a retained-mode
UI toolkit with focus, layout and modal dialogs. Note that Terminal.Gui now ships an optional
`Terminal.Gui.Interop.Spectre` bridge, so Spectre renderables (such as a pretty table in the info
panel) could still be embedded later if they ever become useful.

## Candidates (as of Oct 2026)

| | Terminal.Gui v2 | XenoAtom.Terminal.UI | Consolonia | Hex1b |
| --- | --- | --- | --- | --- |
| Model | Retained-mode views, imperative | Retained-mode, reactive bindings | Avalonia XAML rendered to console | React-style declarative widgets |
| Maturity | v2.0 stable Apr 2026, now 2.5; ~11k stars, ~1.7M downloads | 3.x, fast-moving, small user base | First stable Apr 2026, now 12.0.x | 0.x (pre-1.0) |
| Target | net8+ | net10 only | net8+ | net8+ |
| Controls | 50+ views: TableView, TreeView, menus, dialogs, file dialogs, text editor, markdown | 60+ controls, DataGrid, overlays | Most Avalonia controls | Basic set: list, text, splitter, border |
| Theming | Built-in theme + config system, TrueColor, persistable | Palette-derived themes, alpha blending | Avalonia styles | Built-in themes |
| Key bindings | Command/KeyBinding system, configurable | Yes | Avalonia input | Shortcut bindings |
| Testing | Headless driver, instance-based app model | — | NUnit helper package | Excellent: headless terminal emulator + screen assertions |
| Risk | Low | Medium (young, single-maintainer cadence) | Medium-High (heavy Avalonia stack in a terminal) | High (API still settling) |

## Why Terminal.Gui v2

- **It is the de facto .NET TUI toolkit,** and v2 is a ground-up rewrite that fixed most v1
  complaints: instance-based `IApplication` (no static singletons), TrueColor by default,
  built-in scrolling on every view, decoupled focus/navigation, and adornments
  (margin/border/padding) for clean layout.
- **It has exactly the pieces a commander needs:** modal dialogs, menu bar (for F9), status bar
  (for the function-key strip), text fields with selection (for rename), checkboxes (for F7),
  progress bars (for copy/move), and a configurable command/key-binding layer.
- **Theming and settings are first-class** (`ConfigurationManager`, JSON, persistable), which
  directly supports the "not blue and yellow" requirement and the MVP2 theme picker.
- **Risk is lowest:** large community, regular releases, and v2 is past its long beta.

## Runner-up and when to reconsider

**XenoAtom.Terminal.UI** is the most interesting alternative: reactive state, very efficient
diff-based rendering, synchronized output, and a polished look. Worth revisiting if Terminal.Gui
redraw performance on large directories turns out to be a problem in the spike, or if a
binding-first model ends up being much more pleasant. Its `net10`-only target is fine for us,
but the smaller user base means more unknowns.

**Hex1b** is not a good fit for the app itself yet (pre-1.0), but its headless terminal
emulator and screen assertions could be useful as an *end-to-end test harness* later, driving
the real binary and asserting on screen contents.

**Consolonia** makes sense if you already have Avalonia XAML to reuse. We don't, and running a
full desktop UI stack inside a terminal is more weight than this app needs.

## Spike checklist (before committing)

A short throwaway project in `spike/` to answer these, roughly one or two evenings:

1. **F-keys on macOS terminals.** Confirm F1–F10 and Shift+F-keys reach the app in Ghostty,
   iTerm2, WezTerm and Terminal.app. Terminal.app and macOS itself grab some F-keys (Mission
   Control, Show Desktop), and laptop keyboards need `Fn` unless "Use F1, F2… as standard function
   keys" is on. Validate the fallbacks in [03-mvp0.md](03-mvp0.md#function-keys-on-macos).
2. **Custom panel view performance.** Render a 50k-entry directory in a custom `View` subclass
   that only draws visible rows; scrolling must stay instant.
3. **TableView vs custom View.** Check whether `TableView` can carry Norton-style marking
   (marks independent of the cursor). Expectation: a custom `FilePanelView` is simpler and gives
   full control; `TableView` is the fallback.
4. **Theming.** Define a custom theme in code and JSON, and switch it at runtime.
5. **Native AOT / trimming.** Check that `dotnet publish -p:PublishAot=true` produces a working
   binary (startup time matters for a tool you launch constantly).
6. **Unicode filenames.** macOS stores names in NFD (decomposed) form; confirm accented and
   wide (CJK, emoji) names render and align correctly.

**Exit criterion:** if 1–3 pass, commit to Terminal.Gui v2. If 2 or 3 fail badly, repeat the
same spike with XenoAtom.Terminal.UI before deciding.
