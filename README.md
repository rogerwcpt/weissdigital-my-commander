# weissdigital-my-commander

`myc` is a dual-panel terminal file manager. The plan is in [plan/](plan/README.md).

```bash
dotnet test src/Myc.sln
dotnet run --project src/Myc.App
```

The app shows two panels. The left starts in the current directory and the right starts at home. `Tab` switches panels. `→` or `Enter` opens a directory, `←` returns to it, and `Enter` on a file opens it. `F1` shows the key reference. `F10` or `Ctrl+Q` quits.

## Terminal setup

The commands live on F1–F10. A Mac laptop sends those only while Fn is held, unless the terminal treats them as standard function keys.

- **Ghostty, iTerm2, WezTerm:** turn on “Use F1, F2, … as standard function keys” when F-keys do something else. Option+digit works in terminals that send Option as Meta (`Esc 5` and `Option+5` are both F5).
- **Terminal.app and Mission Control:** System Settings → Keyboard → Keyboard Shortcuts → Mission Control takes several F-keys. Turn off the ones you want `myc` to see, or hold Fn.
- **When a function key never arrives:** press Esc, then the digit. `Esc 1` is Help, `Esc 5` is Copy, `Esc 0` is Quit. Esc followed by any other key clears marks.