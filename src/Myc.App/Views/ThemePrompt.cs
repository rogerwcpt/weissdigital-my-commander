using System.Collections.ObjectModel;
using Myc.App.Theming;
using Myc.Core.Configuration;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Views;

/// <summary>
/// Options → Theme…. Enter applies the highlighted name. Esc closes and leaves the theme alone.
/// </summary>
internal static class ThemePrompt
{
    public static string? Ask(IApplication app, string current)
    {
        string[] names = ThemeNames.All;
        var dialog = new Dialog
        {
            Title = "Theme",
            Width = 34,
            Height = names.Length + 6,
        };
        dialog.SetScheme(Graphite.Bar);
        var list = new ListView
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            Height = Dim.Fill(),
        };
        list.SetSource(new ObservableCollection<string>(names));
        int selected = Array.IndexOf(names, ThemeNames.Canonical(current));
        list.SelectedItem = selected < 0 ? 0 : selected;
        list.KeyDown += (_, key) =>
        {
            if (key != Key.Enter)
            {
                return;
            }

            key.Handled = true;
            dialog.Result = 1;
            dialog.RequestStop();
        };
        dialog.Add(list);
        dialog.Initialized += (_, _) => list.SetFocus();
        dialog.AddButton(new Button { Text = "Cancel", HotKey = Key.Empty });
        dialog.AddButton(new Button { Text = "Apply", IsDefault = true, HotKey = Key.Empty });

        app.Run(dialog);
        if (dialog.Canceled || dialog.Result != 1)
        {
            return null;
        }

        int index = list.SelectedItem ?? -1;
        return index >= 0 && index < names.Length ? names[index] : null;
    }
}
