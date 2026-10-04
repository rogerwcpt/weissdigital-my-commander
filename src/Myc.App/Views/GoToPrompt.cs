using Myc.App.Theming;
using Myc.Core.FileSystem;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Views;

internal readonly record struct GoToAnswer(string Directory, string? SelectName);

/// <summary>
/// Ctrl+G. Tab completes the last segment, Up and Down walk the paths opened earlier, Esc cancels.
/// </summary>
internal static class GoToPrompt
{
    public static GoToAnswer? Ask(
        IApplication app,
        IFileSystem files,
        string current,
        string home,
        bool showHidden,
        IReadOnlyList<string> history)
    {
        var field = new TextField
        {
            Text = current,
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
        };
        var message = new Label
        {
            Text = "Tab completes. Up and Down recall a path.",
            X = 1,
            Y = 2,
            Width = Dim.Fill(1),
        };
        var dialog = new Dialog
        {
            Title = "Go to path",
            Width = 72,
            Height = 9,
        };
        dialog.SetScheme(Graphite.Bar);
        dialog.Add(new Label { Text = "Path", X = 1, Y = 0 }, field, message);

        GoToAnswer? chosen = null;
        int historyIndex = -1;
        string typed = current;
        bool updating = false;
        field.ValueChanged += (_, _) =>
        {
            if (updating)
            {
                return;
            }

            historyIndex = -1;
            typed = field.Text ?? "";
        };
        field.KeyDown += (_, key) =>
        {
            if (key == Key.Tab)
            {
                key.Handled = true;
                string completed = PathInput.Complete(field.Text ?? "", Candidates(files, field.Text ?? "", current, home, showHidden));
                SetText(completed);
                return;
            }

            if (key == Key.CursorUp)
            {
                key.Handled = true;
                Recall(1);
                return;
            }

            if (key == Key.CursorDown)
            {
                key.Handled = true;
                Recall(-1);
            }
        };

        var accept = new Button { Text = "Open", IsDefault = true, HotKey = Key.Empty };
        accept.Accepting += (_, args) =>
        {
            if (Locate(field.Text ?? "", current, home) is not { } answer)
            {
                message.Text = "No such file or directory.";
                args.Handled = true;
                return;
            }

            chosen = answer;
        };
        dialog.AddButton(new Button { Text = "Cancel", HotKey = Key.Empty });
        dialog.AddButton(accept);
        dialog.Initialized += (_, _) => field.SetFocus();
        app.Run(dialog);
        return dialog.Canceled ? null : chosen;

        void SetText(string value)
        {
            updating = true;
            field.Text = value;
            updating = false;
        }

        void Recall(int direction)
        {
            if (history.Count == 0)
            {
                return;
            }

            int next = Math.Clamp(historyIndex + direction, -1, history.Count - 1);
            historyIndex = next;
            SetText(next < 0 ? typed : history[next]);
        }
    }

    private static GoToAnswer? Locate(string text, string current, string home)
    {
        string? full = PathInput.Resolve(text, current, home);
        if (full is null)
        {
            return null;
        }

        if (Directory.Exists(full))
        {
            return new GoToAnswer(full, null);
        }

        if (!File.Exists(full))
        {
            return null;
        }

        string? directory = Path.GetDirectoryName(full);
        string name = Path.GetFileName(full);
        return directory is null || name.Length == 0 ? null : new GoToAnswer(directory, name);
    }

    private static IReadOnlyList<PathMatch> Candidates(
        IFileSystem files,
        string text,
        string current,
        string home,
        bool showHidden)
    {
        string trimmed = text.Trim();
        if (trimmed == "~")
        {
            return [];
        }

        (string directoryText, string fragment) = PathInput.Split(trimmed);
        string? directory = directoryText.Length == 0
            ? current
            : PathInput.Resolve(directoryText, current, home);
        if (directory is null)
        {
            return [];
        }

        bool hidden = showHidden || fragment.StartsWith('.');
        return files.List(directory, hidden).Entries
            .Where(entry => !entry.IsParent)
            .Select(entry => new PathMatch(entry.Name, entry.IsContainer))
            .ToList();
    }
}
