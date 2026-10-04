using System.Diagnostics;
using Myc.App.Theming;
using Myc.Core.Display;
using Myc.Core.Operations;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Views;

internal static class TransferPrompt
{
    public static string? Ask(IApplication app, string verb, int count, string destination)
    {
        string label = count == 1 ? $"{verb} 1 item to:" : $"{verb} {count} items to:";
        NameAnswer answer = NamePrompt.Ask(app, verb, label, verb, destination, selectedGraphemes: 0, error: null);
        return answer.Text;
    }
}

internal static class ConflictPrompt
{
    public static ConflictChoice Ask(IApplication app, ConflictQuestion question)
    {
        (string Label, ConflictChoice Choice)[] options =
        [
            ("Overwrite", ConflictChoice.Overwrite),
            ("Skip", ConflictChoice.Skip),
            ("Rename", ConflictChoice.Rename),
            ("Overwrite all", ConflictChoice.OverwriteAll),
            ("Skip all", ConflictChoice.SkipAll),
            ("Cancel", ConflictChoice.Cancel),
        ];
        var dialog = new Dialog
        {
            Title = "Already exists",
            Width = 78,
            Height = 7,
        };
        dialog.SetScheme(Graphite.Bar);
        dialog.Add(new Label
        {
            Text = question.Name + " already exists.",
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
        });
        foreach ((string label, ConflictChoice choice) in options)
        {
            dialog.AddButton(new Button
            {
                Text = label,
                IsDefault = choice == ConflictChoice.Overwrite,
                HotKey = Key.Empty,
            });
        }

        app.Run(dialog);
        // Canceled is true for every button except the last one, so it cannot tell Overwrite from Esc.
        return dialog.Result is not int index || index < 0 || index >= options.Length
            ? ConflictChoice.Cancel
            : options[index].Choice;
    }
}

internal static class ErrorPrompt
{
    public static ErrorChoice Ask(IApplication app, ErrorQuestion question)
    {
        (string Label, ErrorChoice Choice)[] options =
        [
            ("Retry", ErrorChoice.Retry),
            ("Skip", ErrorChoice.Skip),
            ("Cancel", ErrorChoice.Cancel),
        ];
        var dialog = new Dialog
        {
            Title = "Couldn't read",
            Width = 64,
            Height = 8,
        };
        dialog.SetScheme(Graphite.Bar);
        dialog.Add(new Label
        {
            Text = question.Name + "\n" + question.Message,
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            Height = 2,
        });
        foreach ((string label, ErrorChoice choice) in options)
        {
            dialog.AddButton(new Button
            {
                Text = label,
                IsDefault = choice == ErrorChoice.Cancel,
                HotKey = Key.Empty,
            });
        }

        app.Run(dialog);
        return dialog.Result is not int index || index < 0 || index >= options.Length
            ? ErrorChoice.Cancel
            : options[index].Choice;
    }
}

internal static class DuplicatePrompt
{
    public static bool Ask(IApplication app, IReadOnlyList<string> names)
    {
        string text = names.Count == 1
            ? $"Duplicate as {names[0]}?"
            : $"Duplicate {names.Count} items in this folder?";
        var dialog = new Dialog
        {
            Title = "Same folder",
            Width = 64,
            Height = 7,
        };
        dialog.SetScheme(Graphite.Bar);
        dialog.Add(new Label { Text = text, X = 1, Y = 0, Width = Dim.Fill(1) });
        dialog.AddButton(new Button { Text = "Cancel", HotKey = Key.Empty });
        dialog.AddButton(new Button { Text = "Duplicate", IsDefault = true, HotKey = Key.Empty });
        app.Run(dialog);
        return !dialog.Canceled && dialog.Result == 1;
    }
}

internal sealed class ProgressDialog : Dialog
{
    private readonly Label _current;
    private readonly ProgressBar _bar;
    private readonly Label _detail;
    private readonly Action _cancel;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private JobProgress _latest;
    private bool _cancelled;

    public ProgressDialog(string title, Action cancel)
    {
        Title = title;
        Width = 62;
        Height = 8;
        _cancel = cancel;
        SetScheme(Graphite.Bar);
        _current = new Label { Text = "Scanning…", X = 1, Y = 0, Width = Dim.Fill(1) };
        _bar = new ProgressBar { X = 1, Y = 1, Width = Dim.Fill(1), Height = 1, Fraction = 0 };
        _detail = new Label { Text = "", X = 1, Y = 2, Width = Dim.Fill(1) };
        Add(_current, _bar, _detail);
        var button = new Button { Text = "Cancel", IsDefault = true, HotKey = Key.Empty };
        button.Accepting += (_, args) =>
        {
            Cancel();
            args.Handled = true;
        };
        AddButton(button);
        Initialized += (_, _) => Apply(_latest);
    }

    /// <summary>Esc and the Cancel button. The dialog stays up until the job actually stops.</summary>
    public void Cancel()
    {
        if (_cancelled)
        {
            return;
        }

        _cancelled = true;
        try
        {
            _cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _current.Text = "Cancelling…";
    }

    public void Apply(JobProgress progress)
    {
        _latest = progress;
        if (!IsInitialized)
        {
            return;
        }

        _current.Text = progress.Phase == JobPhase.Scanning || progress.CurrentItem.Length == 0
            ? "Scanning…"
            : progress.CurrentItem;
        float fraction = progress.BytesTotal > 0
            ? (float)progress.BytesDone / progress.BytesTotal
            : progress.ItemsTotal == 0 ? 0 : (float)progress.ItemsDone / progress.ItemsTotal;
        _bar.Fraction = Math.Clamp(fraction, 0, 1);
        string amount = progress.BytesTotal > 0
            ? $"{EntryText.FormatSize(progress.BytesDone)} of {EntryText.FormatSize(progress.BytesTotal)} · "
            : "";
        _detail.Text = $"{progress.ItemsDone} of {progress.ItemsTotal} · {amount}{Elapsed(_clock.Elapsed)}";
    }

    public void Tick()
    {
        if (_latest.ItemsTotal == 0 && _latest.CurrentItem.Length == 0)
        {
            _detail.Text = Elapsed(_clock.Elapsed);
            return;
        }

        Apply(_latest);
    }

    private static string Elapsed(TimeSpan elapsed) =>
        elapsed.TotalHours >= 1 ? elapsed.ToString(@"h\:mm\:ss") : elapsed.ToString(@"m\:ss");
}

internal static class DeletePrompt
{
    public static bool Ask(IApplication app, bool permanent, IReadOnlyList<string> names)
    {
        int count = names.Count;
        string question = permanent
            ? $"Permanently delete {count} {(count == 1 ? "item" : "items")}? This cannot be undone."
            : $"Move {count} {(count == 1 ? "item" : "items")} to Trash?";
        var preview = new List<string> { question, "" };
        preview.AddRange(names.Take(5));
        if (count > 5)
        {
            preview.Add($"and {count - 5} more");
        }

        var dialog = new Dialog
        {
            Title = permanent ? "Delete" : "Trash",
            Width = 64,
            Height = preview.Count + 4,
        };
        dialog.SetScheme(Graphite.Danger);
        dialog.Add(new Label
        {
            Text = string.Join('\n', preview),
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            Height = preview.Count,
        });
        dialog.AddButton(new Button
        {
            Text = permanent ? "Delete" : "Move to Trash",
            HotKey = Key.Empty,
        });
        dialog.AddButton(new Button
        {
            Text = "Cancel",
            IsDefault = true,
            HotKey = Key.Empty,
        });
        app.Run(dialog);
        // The confirm button is first so Enter stays on Cancel. Canceled is true for that first button.
        return dialog.Result == 0;
    }
}
