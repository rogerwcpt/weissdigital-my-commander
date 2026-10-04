using Myc.App.Theming;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Views;

/// <summary>
/// The bottom F-key strip. Unimplemented keys stay visible and dimmed so the Norton
/// layout is there before the commands are.
/// </summary>
internal static class FunctionKeyBar
{
    public static StatusBar Create(IApplication app)
    {
        Shortcut[] keys =
        [
            Item(Key.F1, "Help"),
            Item(Key.F2, "Rename"),
            Item(Key.F3, "—"),
            Item(Key.F4, "—"),
            Item(Key.F5, "Copy"),
            Item(Key.F6, "Move"),
            Item(Key.F7, "MkDir"),
            Item(Key.F8, "Delete"),
            Item(Key.F9, "Menu"),
            Item(Key.F10, "Quit", () => app.RequestStop()),
        ];

        var bar = new StatusBar(keys)
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
            CanFocus = false,
        };
        bar.SetScheme(Graphite.Bar);
        return bar;
    }

    private static Shortcut Item(Key key, string title, Action? action = null)
    {
        return new Shortcut(key, title, action ?? (() => { }), title)
        {
            Enabled = action is not null,
            TabStop = TabBehavior.NoStop,
        };
    }
}
