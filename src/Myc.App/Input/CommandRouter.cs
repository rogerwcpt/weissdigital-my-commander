using Myc.App.Theming;
using Myc.App.Views;
using Myc.Core.Commands;
using Myc.Core.FileSystem;
using Myc.Core.Operations;
using Myc.Core.Platform;
using Myc.Core.Selection;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Input;

/// <summary>
/// App-wide keys: F1, Ctrl+R, and the Esc / Option digit fallbacks.
/// Esc followed by anything other than a digit clears marks, then that key is handled normally.
/// </summary>
internal sealed class CommandRouter
{
    private readonly IApplication _app;
    private readonly Window _window;
    private readonly IFileSystem _files;
    private readonly FilePanelView _left;
    private readonly FilePanelView _right;
    private readonly JobRunner _jobs;
    private readonly ITrash _trash = new MacTrash();
    private bool _escapePending;

    public CommandRouter(IApplication app, Window window, IFileSystem files, FilePanelView left, FilePanelView right)
    {
        _app = app;
        _window = window;
        _files = files;
        _left = left;
        _right = right;
        _jobs = new JobRunner(app);
    }

    public void Invoke(MycCommand command)
    {
        switch (command)
        {
            case MycCommand.Help:
                ShowHelp();
                break;
            case MycCommand.Quit:
                _app.RequestStop();
                break;
            case MycCommand.Rename:
                Rename();
                break;
            case MycCommand.MakeDirectory:
                MakeDirectory();
                break;
            case MycCommand.Copy:
                Transfer(copy: true);
                break;
            case MycCommand.Move:
                Transfer(copy: false);
                break;
            case MycCommand.Delete:
                Delete(permanent: false);
                break;
            case MycCommand.PermanentDelete:
                Delete(permanent: true);
                break;
        }
    }

    public void Attach() => _app.Keyboard.KeyDown += OnKeyDown;

    public void ShowHelp()
    {
        string help = CommandCatalog.HelpText();
        int lines = help.Split('\n').Length;
        var body = new Label
        {
            Text = help,
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            Height = Dim.Fill(1),
        };
        var dialog = new Dialog
        {
            Title = "Keys",
            Width = 72,
            Height = lines + 4,
        };
        dialog.SetScheme(Graphite.Bar);
        dialog.Add(body);
        dialog.AddButton(new Button { Text = "Close", IsDefault = true });
        _app.Run(dialog);
    }

    private void OnKeyDown(object? sender, Key key)
    {
        if (!ReferenceEquals(_app.TopRunnable, _window))
        {
            return;
        }

        if (_escapePending)
        {
            _escapePending = false;
            if (KeyMap.Match(key) is KeyToken pending && CommandCatalog.AsFunctionKey(pending) is KeyToken function)
            {
                key.Handled = true;
                Run(function);
                return;
            }

            ActivePanel()?.ClearMarks();
            if (key == Key.Esc)
            {
                key.Handled = true;
                return;
            }
        }

        if (key == Key.Esc)
        {
            _escapePending = true;
            key.Handled = true;
            return;
        }

        if (KeyMap.Match(key) is not KeyToken pressed)
        {
            return;
        }

        if (CommandCatalog.AsFunctionKey(pressed) is KeyToken aliased)
        {
            key.Handled = true;
            Run(aliased);
            return;
        }

        if (pressed == KeyToken.ShiftF8)
        {
            key.Handled = true;
            Invoke(MycCommand.PermanentDelete);
            return;
        }

        if (pressed == KeyToken.CtrlR)
        {
            key.Handled = true;
            ActivePanel()?.Refresh();
        }
    }

    private void Run(KeyToken functionKey)
    {
        if (CommandCatalog.Find(functionKey) is not { Available: true } spec)
        {
            return;
        }

        Invoke(spec.Command);
    }

    private void Rename()
    {
        if (ActivePanel() is not { } panel)
        {
            return;
        }

        if (panel.CursorEntry is not { } entry || entry.IsParent)
        {
            panel.ShowError("Can't rename that.");
            return;
        }

        string typed = entry.Name;
        string? error = null;
        while (true)
        {
            NameAnswer answer = NamePrompt.Ask(_app, "Rename", "New name:", "Rename", typed, EntryNames.BasenameGraphemes(typed), error);
            if (answer.Text is null)
            {
                return;
            }

            string next = answer.Text;

            FileChange change = _files.Rename(panel.Directory, entry.Name, next);
            if (change.Succeeded)
            {
                RefreshAffected(panel, change.SelectedName, entry.Name, change.SelectedName);
                return;
            }

            typed = next;
            error = change.Error;
        }
    }

    private void MakeDirectory()
    {
        if (ActivePanel() is not { } panel)
        {
            return;
        }

        if (_jobs.IsRunning)
        {
            panel.ShowError("Wait for the current operation to finish.");
            return;
        }

        string typed = "";
        string? error = null;
        while (true)
        {
            FileEntry[] marked = Marked(panel);
            NameAnswer answer = NamePrompt.Ask(
                _app,
                "New folder",
                "New folder name:",
                "Create",
                typed,
                0,
                error,
                offerMove: marked.Length > 0,
                markedCount: marked.Length);
            if (answer.Text is null)
            {
                return;
            }

            FileChange change = _files.CreateDirectory(panel.Directory, answer.Text);
            if (!change.Succeeded)
            {
                typed = answer.Text;
                error = change.Error;
                continue;
            }

            if (!answer.MoveMarked)
            {
                RefreshAffected(panel, change.SelectedName, null, null);
                return;
            }

            string target = Path.GetFullPath(Path.Combine(panel.Directory, answer.Text.Trim().TrimEnd('/')));
            StartTransfer(
                new MoveJob(marked.Select(entry => entry.FullPath).ToArray(), target),
                "Move",
                result => FinishTransfer(panel, change.SelectedName, result, clearMarks: result.Status == JobStatus.Completed));
            return;
        }
    }

    private void Transfer(bool copy)
    {
        if (ActivePanel() is not { } panel)
        {
            return;
        }

        if (_jobs.IsRunning)
        {
            panel.ShowError("Wait for the current operation to finish.");
            return;
        }

        FileEntry[] selection = EffectiveSelection.Resolve(panel.Entries, panel.Marks, panel.CursorIndex).ToArray();
        if (selection.Length == 0)
        {
            panel.ShowError(copy ? "Nothing to copy." : "Nothing to move.");
            return;
        }

        string? destination = TransferPrompt.Ask(_app, copy ? "Copy" : "Move", selection.Length, Other(panel).Directory);
        if (destination is null)
        {
            return;
        }

        destination = ExpandHome(destination.Trim());
        if (destination.Length == 0)
        {
            panel.ShowError("Enter a folder.");
            return;
        }

        string[] paths = selection.Select(entry => entry.FullPath).ToArray();
        bool duplicate = false;
        Run();

        void Run()
        {
            IFileJob job = copy
                ? new CopyJob(paths, destination, duplicate)
                : new MoveJob(paths, destination, duplicate);
            StartTransfer(job, copy ? "Copy" : "Move", result =>
            {
                if (result.Status == JobStatus.Refused && result.DuplicateNames.Count > 0 && !duplicate)
                {
                    if (DuplicatePrompt.Ask(_app, result.DuplicateNames))
                    {
                        duplicate = true;
                        Run();
                    }

                    return;
                }

                FinishTransfer(panel, selectName: null, result, clearMarks: result.Status == JobStatus.Completed);
            });
        }
    }

    private void Delete(bool permanent)
    {
        if (ActivePanel() is not { } panel)
        {
            return;
        }

        if (_jobs.IsRunning)
        {
            panel.ShowError("Wait for the current operation to finish.");
            return;
        }

        FileEntry[] selection = EffectiveSelection.Resolve(panel.Entries, panel.Marks, panel.CursorIndex).ToArray();
        if (selection.Length == 0)
        {
            panel.ShowError("Nothing to delete.");
            return;
        }

        if (!DeletePrompt.Ask(_app, permanent, selection.Select(entry => entry.Name).ToArray()))
        {
            return;
        }

        var job = new DeleteJob(
            selection.Select(entry => entry.FullPath).ToArray(),
            permanent,
            permanent ? null : _trash);
        StartTransfer(
            job,
            permanent ? "Delete" : "Trash",
            result => FinishTransfer(panel, selectName: null, result, clearMarks: result.Status == JobStatus.Completed));
    }

    private void StartTransfer(IFileJob job, string title, Action<JobResult> completed)
    {
        if (_jobs.IsRunning)
        {
            ActivePanel()?.ShowError("Wait for the current operation to finish.");
            return;
        }

        _jobs.Start(job, title, completed);
    }

    private void FinishTransfer(FilePanelView source, string? selectName, JobResult result, bool clearMarks)
    {
        if (clearMarks)
        {
            source.ClearMarks();
        }

        if (selectName is null)
        {
            source.ReloadKeepingCursor();
        }
        else
        {
            source.Reload(selectName);
        }

        Other(source).ReloadKeepingCursor();
        if (result.Message is not null && result.Status is JobStatus.Failed or JobStatus.Refused)
        {
            source.ShowError(result.Message);
        }
    }

    private FilePanelView Other(FilePanelView panel) => ReferenceEquals(panel, _left) ? _right : _left;

    private static FileEntry[] Marked(FilePanelView panel) =>
        panel.Entries.Where(entry => !entry.IsParent && panel.Marks.Contains(entry.Name)).ToArray();

    private static string ExpandHome(string path)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path == "~")
        {
            return home;
        }

        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            return Path.Combine(home, path[2..]);
        }

        return path;
    }

    private void RefreshAffected(FilePanelView origin, string? activeSelection, string? fromName, string? toName)
    {
        foreach (FilePanelView panel in new[] { _left, _right })
        {
            if (!string.Equals(panel.Directory, origin.Directory, StringComparison.Ordinal))
            {
                continue;
            }

            if (fromName is not null && toName is not null)
            {
                panel.ReplaceMark(fromName, toName);
            }

            string? select = activeSelection;
            if (!ReferenceEquals(panel, origin))
            {
                string? cursor = panel.CursorEntry?.Name;
                select = cursor == fromName ? toName : cursor;
            }

            panel.Reload(select);
        }
    }

    private FilePanelView? ActivePanel() => _window.MostFocused as FilePanelView;
}
