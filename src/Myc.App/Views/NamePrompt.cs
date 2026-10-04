using Myc.App.Theming;
using Myc.Core.FileSystem;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Views;

internal readonly record struct NameAnswer(string? Text, bool MoveMarked);

/// <summary>One text field and a short error line. Text is null when cancelled.</summary>
internal static class NamePrompt
{
    public static NameAnswer Ask(
        IApplication app,
        string title,
        string label,
        string confirm,
        string initial,
        int selectedGraphemes,
        string? error,
        bool offerMove = false,
        int markedCount = 0)
    {
        var field = new BasenameTextField
        {
            Text = initial,
            SelectedGraphemes = selectedGraphemes,
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
            Title = title,
            Width = 64,
            Height = offerMove ? 12 : 9,
        };
        dialog.SetScheme(Graphite.Bar);
        dialog.Add(new Label { Text = label, X = 1, Y = 0 }, field, message);
        CheckBox? move = null;
        if (offerMove)
        {
            move = new CheckBox
            {
                Text = "Move selection into new folder?",
                X = 1,
                Y = 3,
                Width = Dim.Fill(1),
            };
            string items = markedCount == 1 ? "1 item marked" : $"{markedCount} items marked";
            dialog.Add(move, new Label { Text = items, X = 1, Y = 4, Width = Dim.Fill(1) });
        }

        var cancel = new Button { Text = "Cancel", HotKey = Key.Empty };
        var accept = new Button { Text = confirm, IsDefault = true, HotKey = Key.Empty };
        dialog.AddButton(cancel);
        dialog.AddButton(accept);
        dialog.Initialized += (_, _) => field.SetFocus();

        app.Run(dialog);
        if (dialog.Canceled)
        {
            return new NameAnswer(null, false);
        }

        return new NameAnswer(field.Text, move?.Value == CheckState.Checked);
    }
}

/// <summary>Selects the base name once the field takes focus. Terminal.Gui otherwise selects all of it.</summary>
internal sealed class BasenameTextField : TextField
{
    public int SelectedGraphemes { get; set; }

    protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView)
    {
        base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);
        if (!newHasFocus || string.IsNullOrEmpty(Text))
        {
            return;
        }

        if (SelectedGraphemes == 0)
        {
            ClearAllSelection();
            InsertionPoint = EntryNames.GraphemeCount(Text);
            return;
        }

        int all = EntryNames.GraphemeCount(Text);
        if (SelectedGraphemes <= 0 || SelectedGraphemes >= all)
        {
            return;
        }

        InsertionPoint = SelectedGraphemes;
        SelectedStart = 0;
    }
}
