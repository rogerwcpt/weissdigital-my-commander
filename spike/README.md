# myc framework spike

Throwaway Terminal.Gui 2.5 probe for the checklist in [plan/01-framework.md](../plan/01-framework.md).
Delete `spike/` once the framework decision is recorded.

```bash
dotnet run --project spike/Myc.Spike
dotnet run --project spike/Myc.Spike -- --self-check
dotnet publish spike/Myc.Spike -c Release -p:PublishAot=true
```

`F10` or `Ctrl+Q` quits. `Esc` does not quit, so the Esc+digit fallback can be tested.

## What is on screen

- **Left:** a custom panel over 50,000 synthetic entries. It draws only the visible rows.
  `Space` toggles a mark and moves the cursor down. The title shows the last paint time.
- **Right:** a `TableView` of the same entries, with Terminal.Gui's checkbox column.
  `Space` toggles the checkbox. Marks are `CheckedRows`, separate from the cursor.
- **Top:** every key the driver delivers, plus which F-keys have been seen.
- **`t`:** cycles three themes. Code Graphite (a `Scheme` built in C#), JSON Graphite, JSON Paper.
- **`F5` / `F8`:** modal dialogs. They do not touch the disk.
- **`Esc` then a digit:** logged as the Midnight Commander fallback (`Esc 5` → F5). It does not run the command.

The first rows are the Unicode cases: NFC and NFD `café` / `naïve`, CJK `文件.txt` and `写真フォルダ`, and `reef-🐠.jpg`.

## Automated results (4 Oct 2026, Terminal.Gui 2.5.0, .NET 10)

`dotnet run -- --self-check` passed.

| Check | Result |
| --- | --- |
| 50k rows, format one 40×60 screen | About 130µs of CPU, before any terminal I/O. Fast enough that drawing is not the risk; the terminal write is. |
| CJK width | `文件.txt` is 8 columns (two wide characters + `.txt`). `写真フォルダ` is 12. |
| Emoji width | `reef-🐠.jpg` is 11 columns, so the fish is width 2. |
| NFD vs NFC | The strings are not equal (`café` vs `cafe` + combining acute) but both measure 8 columns. Comparing names later must go through `string.Normalize`. |
| JSON theme | `themes/paper.json` applies. Paper is `#1F2328` on `#F6F8FA`; Graphite is `#C9CCD1` on `#1E2127` with teal `#4FB8A8`. |
| `SwitchTheme` | Returns false for those custom names. They are not in `ThemeNames` (that list is the built-in themes only). Switching works by setting `RuntimeConfig` and calling `ApplyToStaticFacades` again. |
| `Key` | A class, so `key.Handled = true` in the app `KeyDown` handler actually consumes the key. |

## Still needs a terminal

Run `dotnet run --project spike/Myc.Spike` in Ghostty, iTerm2, WezTerm, and Terminal.app.

1. Press F1–F10 and Shift+F8. The top line should name the key. Note any key macOS or the terminal swallows.
2. Press `Esc` then `5`. The log should say `Esc 5 → F5`.
3. Hold Down on the left panel. The title's paint time should stay well under a millisecond and scrolling should not stutter.
4. `Space` on the left marks and moves down. `Space` on the right toggles the checkbox and should leave the cursor where it is. That gap is why MVP0 wants the custom panel.
5. Press `t` three times and confirm Graphite, then the light Paper theme.
6. Look at the first rows. NFD `café` should look the same as NFC and line up with the rows around it. The fish emoji should not shift the following columns.

## Native AOT

`dotnet publish -c Release -p:PublishAot=true` succeeded with no warnings. The publish
folder holds an 8.5 MB `myc-spike` plus `libonigwrap.dylib` (Oniguruma, pulled in by
Terminal.Gui). It is not a single file. The binary's `--self-check` passed in 0.11s,
and formatting one screen dropped to about 24µs.

A Homebrew formula later needs to install the dylib next to `myc`, or the publish has
to be switched to static linking. Startup is already in the range you want for a tool
you launch constantly.
