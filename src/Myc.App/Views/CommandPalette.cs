using System.Collections.ObjectModel;
using Myc.App.Theming;
using Myc.Core.Commands;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Views;

/// <summary>
/// Ctrl+Shift+P and Ctrl+P. Type to narrow the list. Enter runs the row. Esc closes it.
/// </summary>
internal static class CommandPalette
{
    private const int LabelWidth = 18;
    private const int KeyWidth = 14;

    public static MycCommand? Ask(IApplication app)
    {
        CommandSpec[] all = CommandCatalog.Palette.ToArray();
        CommandSpec[] shown = all;
        MycCommand? chosen = null;
        var field = new TextField
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
        };
        var list = new ListView
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill(1),
            Height = Dim.Fill(1),
        };
        var dialog = new Dialog
        {
            Title = "Commands",
            Width = WidthFor(all),
            Height = 16,
        };
        dialog.SetScheme(Graphite.Bar);
        field.SetScheme(Graphite.Bar);
        list.SetScheme(Graphite.Bar);

        void Show(CommandSpec[] next)
        {
            shown = next;
            list.SetSource(new ObservableCollection<string>(next.Select(Row)));
            if (next.Length > 0)
            {
                list.SelectedItem = 0;
            }
        }

        void Accept()
        {
            int index = list.SelectedItem ?? -1;
            if (index < 0 || index >= shown.Length)
            {
                return;
            }

            chosen = shown[index].Command;
            dialog.RequestStop();
        }

        void Move(int delta)
        {
            if (shown.Length == 0)
            {
                return;
            }

            int index = list.SelectedItem ?? 0;
            int next = index + delta;
            if (next < 0)
            {
                next = 0;
            }
            else if (next >= shown.Length)
            {
                next = shown.Length - 1;
            }

            list.SelectedItem = next;
        }

        field.ValueChanged += (_, _) =>
        {
            string query = (field.Text ?? "").Trim();
            Show(query.Length == 0
                ? all
                : all.Where(spec => Matches(spec, query)).ToArray());
        };
        field.KeyDown += (_, key) =>
        {
            if (key == Key.CursorDown)
            {
                key.Handled = true;
                Move(1);
                return;
            }

            if (key == Key.CursorUp)
            {
                key.Handled = true;
                Move(-1);
                return;
            }

            if (key == Key.Enter)
            {
                key.Handled = true;
                Accept();
            }
        };
        list.KeyDown += (_, key) =>
        {
            if (key != Key.Enter)
            {
                return;
            }

            key.Handled = true;
            Accept();
        };

        dialog.Add(field, list);
        dialog.Initialized += (_, _) => field.SetFocus();
        Show(all);
        app.Run(dialog);
        return chosen;
    }

    private static bool Matches(CommandSpec spec, string query) =>
        spec.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
        || spec.Summary.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string Row(CommandSpec spec)
    {
        string key = spec.Keys.Length == 0 ? "" : CommandCatalog.Display(spec.Keys[0]);
        return $"{spec.Label,-LabelWidth}{key,-KeyWidth}{spec.Summary}";
    }

    /// <summary>
    /// Wide enough for the longest row. The extra cells are the border, the list inset, and a gap before the edge.
    /// </summary>
    private static int WidthFor(CommandSpec[] commands)
    {
        int longest = 0;
        foreach (CommandSpec spec in commands)
        {
            longest = Math.Max(longest, Row(spec).Length);
        }

        return longest + 6;
    }
}
