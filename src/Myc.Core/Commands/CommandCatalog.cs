namespace Myc.Core.Commands;

public sealed record CommandSpec(MycCommand Command, string Label, string Summary, bool Available, params KeyToken[] Keys);

/// <summary>
/// Every command and the keys that invoke it. The function-key bar, F1, and the
/// Esc / Option digit fallbacks all read this list.
/// </summary>
public static class CommandCatalog
{
    private static readonly KeyToken[] FunctionKeys =
    [
        KeyToken.F1, KeyToken.F2, KeyToken.F3, KeyToken.F4, KeyToken.F5,
        KeyToken.F6, KeyToken.F7, KeyToken.F8, KeyToken.F9, KeyToken.F10,
    ];

    public static IReadOnlyList<CommandSpec> All { get; } =
    [
        new(MycCommand.Help, "Help", "Show this key reference", true, KeyToken.F1),
        new(MycCommand.Rename, "Rename", "Rename the item under the cursor", true, KeyToken.F2),
        new(MycCommand.View, "—", "Reserved", false, KeyToken.F3),
        new(MycCommand.Edit, "—", "Reserved", false, KeyToken.F4),
        new(MycCommand.Copy, "Copy", "Copy the selection to the other panel", true, KeyToken.F5),
        new(MycCommand.Move, "Move", "Move the selection to the other panel", true, KeyToken.F6),
        new(MycCommand.MakeDirectory, "MkDir", "Create a directory", true, KeyToken.F7),
        new(MycCommand.Delete, "Delete", "Move the selection to Trash", true, KeyToken.F8),
        new(MycCommand.Menu, "Menu", "Open the menu", false, KeyToken.F9),
        new(MycCommand.Quit, "Quit", "Quit", true, KeyToken.F10, KeyToken.CtrlQ),
        new(MycCommand.PermanentDelete, "Delete", "Permanently delete the selection", true, KeyToken.ShiftF8),
        new(MycCommand.Refresh, "Refresh", "Reload this panel", true, KeyToken.CtrlR),
        new(MycCommand.ClearMarks, "Unmark", "Clear marks", true, KeyToken.Esc),
        new(MycCommand.SwitchPanel, "Panel", "Switch the active panel", true, KeyToken.Tab),
        new(MycCommand.Mark, "Mark", "Mark and move down", true, KeyToken.Space, KeyToken.Insert),
        new(MycCommand.MoveCursor, "Cursor", "Move the cursor", true, KeyToken.Up, KeyToken.Down, KeyToken.PageUp, KeyToken.PageDown, KeyToken.Home, KeyToken.End),
        new(MycCommand.EnterDirectory, "Open", "Enter a directory", true, KeyToken.Right),
        new(MycCommand.ParentDirectory, "Parent", "Go up, with the cursor on the folder you left", true, KeyToken.Left),
        new(MycCommand.Activate, "Open", "Enter a directory, or open a file", true, KeyToken.Enter),
    ];

    public static IReadOnlyList<CommandSpec> Bar { get; } =
        FunctionKeys.Select(key => Find(key)!).ToArray();

    public static CommandSpec? Find(KeyToken key) =>
        All.FirstOrDefault(spec => spec.Keys.Contains(key));

    /// <summary>
    /// Esc then a digit, or Option+digit, is the matching function key.
    /// 1–9 are F1–F9 and 0 is F10. A real function key is not an alias of itself.
    /// </summary>
    public static KeyToken? AsFunctionKey(KeyToken key) => key switch
    {
        KeyToken.D1 or KeyToken.Alt1 => KeyToken.F1,
        KeyToken.D2 or KeyToken.Alt2 => KeyToken.F2,
        KeyToken.D3 or KeyToken.Alt3 => KeyToken.F3,
        KeyToken.D4 or KeyToken.Alt4 => KeyToken.F4,
        KeyToken.D5 or KeyToken.Alt5 => KeyToken.F5,
        KeyToken.D6 or KeyToken.Alt6 => KeyToken.F6,
        KeyToken.D7 or KeyToken.Alt7 => KeyToken.F7,
        KeyToken.D8 or KeyToken.Alt8 => KeyToken.F8,
        KeyToken.D9 or KeyToken.Alt9 => KeyToken.F9,
        KeyToken.D0 or KeyToken.Alt0 => KeyToken.F10,
        _ => null,
    };

    public static string HelpText()
    {
        var lines = new List<string>();
        for (int i = 0; i < FunctionKeys.Length; i += 3)
        {
            lines.Add(string.Join("  ", FunctionKeys.Skip(i).Take(3).Select(Cell)));
        }

        lines.Add($"{Display(KeyToken.ShiftF8),-10} {Find(KeyToken.ShiftF8)!.Summary}");
        lines.Add("Esc, then 1-9 or 0, does the same as F1-F10. Option+digit does the same.");
        lines.Add("");

        foreach (CommandSpec spec in All)
        {
            KeyToken[] extra = spec.Keys.Where(key => !FunctionKeys.Contains(key) && key != KeyToken.ShiftF8).ToArray();
            if (extra.Length == 0)
            {
                continue;
            }

            lines.Add($"{string.Join(", ", extra.Select(Display)),-18} {spec.Summary}");
        }

        return string.Join('\n', lines);
    }

    public static string Display(KeyToken key) => key switch
    {
        KeyToken.ShiftF8 => "Shift+F8",
        KeyToken.CtrlQ => "Ctrl+Q",
        KeyToken.CtrlR => "Ctrl+R",
        KeyToken.Esc => "Esc",
        KeyToken.Tab => "Tab",
        KeyToken.Space => "Space",
        KeyToken.Insert => "Insert",
        KeyToken.Up => "Up",
        KeyToken.Down => "Down",
        KeyToken.Left => "Left",
        KeyToken.Right => "Right",
        KeyToken.PageUp => "PgUp",
        KeyToken.PageDown => "PgDn",
        KeyToken.Home => "Home",
        KeyToken.End => "End",
        KeyToken.Enter => "Enter",
        _ => key.ToString(),
    };

    private static string Cell(KeyToken key)
    {
        CommandSpec spec = Find(key)!;
        return $"{Display(key),-4} {spec.Label,-8}";
    }
}
