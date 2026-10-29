using Myc.App.Theming;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Views;

/// <summary>One field for a pasted token. Null when cancelled.</summary>
internal static class TokenPrompt
{
    public static string? Ask(IApplication app, string initial, string? error)
    {
        var field = new TextField
        {
            Text = initial,
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
        };
        var message = new Label
        {
            Text = error ?? "",
            X = 1,
            Y = 2,
            Width = Dim.Fill(1),
            Height = 1,
        };
        var dialog = new Dialog
        {
            Title = "JWT",
            Width = 72,
            Height = 9,
        };
        dialog.SetScheme(Graphite.Bar);
        dialog.Add(new Label { Text = "Token:", X = 1, Y = 0 }, field, message);
        dialog.AddButton(new Button { Text = "Cancel", HotKey = Key.Empty });
        dialog.AddButton(new Button { Text = "View", IsDefault = true, HotKey = Key.Empty });
        dialog.Initialized += (_, _) => field.SetFocus();
        app.Run(dialog);
        return dialog.Canceled ? null : field.Text ?? "";
    }
}
