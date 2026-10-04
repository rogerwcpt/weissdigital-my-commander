using Myc.App.Theming;
using Myc.Core.FileSystem;
using Myc.Core.Selection;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Views;

/// <summary>
/// <c>+</c> and <c>-</c>. The count updates as the pattern is typed. Enter applies it, Esc leaves the marks alone.
/// </summary>
internal static class PatternPrompt
{
    public static string? Ask(IApplication app, string title, string confirm, IReadOnlyList<FileEntry> entries)
    {
        var hint = new Label
        {
            Text = Describe(entries, "*"),
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
        };
        var field = new TextField
        {
            Text = "*",
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
        };
        field.ValueChanged += (_, _) => hint.Text = Describe(entries, field.Text);
        var dialog = new Dialog
        {
            Title = title,
            Width = 52,
            Height = 8,
        };
        dialog.SetScheme(Graphite.Bar);
        dialog.Add(hint, field);
        dialog.AddButton(new Button { Text = "Cancel", HotKey = Key.Empty });
        dialog.AddButton(new Button { Text = confirm, IsDefault = true, HotKey = Key.Empty });
        dialog.Initialized += (_, _) => field.SetFocus();

        app.Run(dialog);
        if (dialog.Canceled)
        {
            return null;
        }

        string pattern = field.Text?.Trim() ?? "";
        return pattern.Length == 0 ? null : pattern;
    }

    private static string Describe(IReadOnlyList<FileEntry> entries, string? pattern)
    {
        int count = NamePattern.Count(entries, pattern);
        string matches = count == 1 ? "1 match" : $"{count} matches";
        return $"{matches}. For example *.jpg";
    }
}
