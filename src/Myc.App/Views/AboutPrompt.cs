using Myc.App.Theming;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Views;

internal static class AboutPrompt
{
    public static void Show(IApplication app)
    {
        const string text = "myc\nA dual-panel file manager.\nWeiss Digital";
        var dialog = new Dialog
        {
            Title = "About",
            Width = 42,
            Height = 8,
        };
        dialog.SetScheme(Graphite.Bar);
        dialog.Add(new Label
        {
            Text = text,
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            Height = 3,
        });
        dialog.AddButton(new Button { Text = "Close", IsDefault = true, HotKey = Key.Empty });
        app.Run(dialog);
    }
}
