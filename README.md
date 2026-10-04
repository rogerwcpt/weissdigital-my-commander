# weissdigital-my-commander

`myc` is a dual-panel terminal file manager. The plan is in [plan/](plan/README.md).

```bash
dotnet test src/Myc.sln
dotnet run --project src/Myc.App
```

The app shows two panels. The left starts in the current directory and the right starts at home. `Tab` switches panels. `→` or `Enter` opens a directory, `←` returns to it, and `Enter` on a file opens it. `F2` renames the cursor item. `F5` copies the selection to the other panel and `F6` moves it. `F7` creates a folder (`a/b/c` creates the missing parents); when items are marked it can move them into that folder. `F8` moves the selection to Trash. `Shift+F8` deletes it permanently. `F1` shows the key reference. `F10` or `Ctrl+Q` quits.

## Terminal setup

The commands live on F1–F10. A Mac laptop sends those only while Fn is held, unless the terminal treats them as standard function keys.

- **Ghostty, iTerm2, WezTerm:** turn on “Use F1, F2, … as standard function keys” when F-keys do something else. Option+digit works in terminals that send Option as Meta (`Esc 5` and `Option+5` are both F5).
- **Terminal.app and Mission Control:** System Settings → Keyboard → Keyboard Shortcuts → Mission Control takes several F-keys. Turn off the ones you want `myc` to see, or hold Fn.
- **When a function key never arrives:** press Esc, then the digit. `Esc 1` is Help, `Esc 5` is Copy, `Esc 0` is Quit. Esc followed by any other key clears marks.
- **`NO_COLOR`:** set it to anything other than empty (`NO_COLOR=1`) and myc keeps the terminal's own colours. Marks stay `•`, directories stay `▸`, the cursor is inverse, and directories are bold.

## Terminal check

Resize the window, and look at one panel that has a long name, a `café` stored by macOS (decomposed), a CJK name, and an emoji. The same checks in Ghostty, iTerm2, and Terminal.app:

| Check | What you should see |
| --- | --- |
| Wide window | Name, Size, and Modified all fit, and the columns line up. |
| Narrower | Modified disappears first. Narrower still, Size goes too, and the name keeps the width. |
| Long name | The row keeps the start of the name and the extension, with `…` between them. The size column does not shift. |
| `café`, CJK, emoji | The accent looks like one letter, wide characters take two columns, and the size column still lines up. |
| `NO_COLOR=1` | No teal and no red. The active panel border is bold, the cursor row is inverse, and marks are still `•`. |
| Keys | F1–F10, Shift+F8, and Esc then a digit. Note any key Mission Control or the terminal swallows. |