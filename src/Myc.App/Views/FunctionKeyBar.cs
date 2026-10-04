using Myc.App.Input;
using Myc.App.Theming;
using Myc.Core.Commands;
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
    public static StatusBar Create(IApplication app, Action showHelp)
    {
        Shortcut[] keys = CommandCatalog.Bar.Select(spec =>
        {
            Action? action = spec.Command switch
            {
                MycCommand.Help => showHelp,
                MycCommand.Quit => () => app.RequestStop(),
                _ => null,
            };
            return Item(KeyMap.ToGui(spec.Keys[0]), spec.Label, spec.Summary, action);
        }).ToArray();

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

    private static Shortcut Item(Key key, string title, string help, Action? action)
    {
        return new Shortcut(key, title, action ?? (() => { }), help)
        {
            Enabled = action is not null,
            TabStop = TabBehavior.NoStop,
        };
    }
}
