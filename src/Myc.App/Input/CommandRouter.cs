using Myc.App.Theming;
using Myc.App.Views;
using Myc.Core.Commands;
using Myc.Core.Configuration;
using Myc.Core.FileSystem;
using Myc.Core.Sorting;
using Myc.Core.Operations;
using Myc.Core.Platform;
using Myc.Core.Selection;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Input;

/// <summary>
/// App-wide keys: F9 opens the menu, F1, Ctrl+R, and the Esc / Option digit fallbacks.
/// Esc followed by anything other than a digit clears marks, then that key is handled normally.
/// While the menu is open, Esc and F9 close it and every other key stays with the menu.
/// While a dialog is open, Esc closes it as Cancel and does not run the confirming button.
/// </summary>
internal sealed class CommandRouter
{
    private readonly IApplication _app;
    private readonly Window _window;
    private readonly IFileSystem _files;
    private readonly FilePanelView _left;
    private readonly FilePanelView _right;
    private readonly JobRunner _jobs;
    private readonly IFileOpener _opener;
    private readonly Func<string> _editor;
    private readonly ITrash _trash = new MacTrash();
    private readonly List<string> _gotoHistory = [];
    private Func<ConfirmationSettings> _confirm = () => ConfirmationSettings.Default;
    private int _viewOpening;
    private FilePanelView _active;
    private AppMenu? _menu;
    private bool _escapePending;
    private object? _jumpTimer;

    public CommandRouter(
        IApplication app,
        Window window,
        IFileSystem files,
        FilePanelView left,
        FilePanelView right,
        IFileOpener opener,
        Func<string> editor)
    {
        _app = app;
        _window = window;
        _files = files;
        _left = left;
        _right = right;
        _opener = opener;
        _editor = editor;
        _active = left;
        _jobs = new JobRunner(app, () => _confirm().Overwrite, () => ReferenceEquals(app.TopRunnable, window));
        left.BecameFocused += NoteFocus;
        right.BecameFocused += NoteFocus;
    }

    public void UseMenu(AppMenu menu) => _menu = menu;

    public void UseConfirmations(Func<ConfirmationSettings> confirm) => _confirm = confirm;

    public void Invoke(MycCommand command)
    {
        ClearJump();
        if (command != MycCommand.Menu)
        {
            _menu?.Close();
        }

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
            case MycCommand.Menu:
                FilePanelView current = ActivePanel();
                _menu?.Reflect(current.Sort, current.SortDescending, current.ShowHidden, _confirm());
                _menu?.Toggle();
                break;
            case MycCommand.SortName:
                ActivePanel().SortBy(PanelSort.Name);
                break;
            case MycCommand.SortExtension:
                ActivePanel().SortBy(PanelSort.Extension);
                break;
            case MycCommand.SortSize:
                ActivePanel().SortBy(PanelSort.Size);
                break;
            case MycCommand.SortModified:
                ActivePanel().SortBy(PanelSort.Modified);
                break;
            case MycCommand.ToggleHidden:
                ActivePanel().ToggleHidden();
                break;
            case MycCommand.SelectPattern:
                SelectByPattern(add: true);
                break;
            case MycCommand.DeselectPattern:
                SelectByPattern(add: false);
                break;
            case MycCommand.InvertSelection:
                ActivePanel().InvertMarks();
                break;
            case MycCommand.SelectAll:
                ActivePanel().SelectAll();
                break;
            case MycCommand.GoToPath:
                GoToPath();
                break;
            case MycCommand.SameDirectory:
                SameDirectory();
                break;
            case MycCommand.SwapPanels:
                SwapPanels();
                break;
            case MycCommand.Reveal:
                ActivePanel().RevealCursor();
                break;
            case MycCommand.Shell:
                OpenShell();
                break;
            case MycCommand.View:
                ViewFile();
                break;
            case MycCommand.Edit:
                EditFile();
                break;
            case MycCommand.CommandPalette:
                ShowPalette();
                break;
            case MycCommand.CopyPath:
                CopyPath();
                break;
            case MycCommand.CopyName:
                CopyName();
                break;
            case MycCommand.NewFile:
                NewFile();
                break;
            case MycCommand.Refresh:
                ActivePanel().Refresh();
                break;
            case MycCommand.ClearMarks:
                ActivePanel().ClearMarks();
                break;
            case MycCommand.Mark:
                ActivePanel().ToggleMark();
                break;
            case MycCommand.Activate:
                ActivePanel().ActivateSelection();
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

    /// <summary>
    /// Esc on a dialog leaves <see cref="Dialog.Result"/> unset, which every prompt already treats as Cancel.
    /// A progress dialog stays open and stops the job, the same as its Cancel button.
    /// </summary>
    private void DismissDialog()
    {
        if (_app.TopRunnable is ProgressDialog progress)
        {
            progress.Cancel();
            return;
        }

        if (_app.TopRunnable is Runnable runnable)
        {
            runnable.RequestStop();
        }
    }

    private void OnKeyDown(object? sender, Key key)
    {
        if (_app.TopRunnable is FileViewer viewer)
        {
            if (key == Key.Esc || key == Key.F3)
            {
                key.Handled = true;
                viewer.RequestStop();
                return;
            }

            if (key == Key.F10 || (key == Key.Q.WithCtrl && !key.IsAlt && !key.IsShift))
            {
                key.Handled = true;
                viewer.RequestStop();
                _app.RequestStop(_window);
                return;
            }

            if (IsPlainLetter(key, 'w'))
            {
                key.Handled = true;
                viewer.ToggleWrap();
                return;
            }

            if (IsPlainLetter(key, 'm'))
            {
                key.Handled = true;
                viewer.ToggleMarkdown();
                return;
            }

            return;
        }

        if (!ReferenceEquals(_app.TopRunnable, _window))
        {
            if (key == Key.Esc)
            {
                key.Handled = true;
                DismissDialog();
            }

            return;
        }

        if (_menu is { IsOpen: true })
        {
            if (key == Key.Esc || key == Key.F9)
            {
                key.Handled = true;
                _menu.Close();
            }

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
            if (HasJump)
            {
                ClearJump();
            }

            _escapePending = true;
            key.Handled = true;
            return;
        }

        if (TryTypeJump(key))
        {
            key.Handled = true;
            return;
        }

        if (HasJump)
        {
            ClearJump();
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
            ActivePanel().Refresh();
            return;
        }

        if (pressed == KeyToken.F9)
        {
            key.Handled = true;
            Invoke(MycCommand.Menu);
            return;
        }

        if (pressed is KeyToken.CtrlF3 or KeyToken.CtrlF4 or KeyToken.CtrlF5 or KeyToken.CtrlF6 or KeyToken.AltPeriod
            or KeyToken.Plus or KeyToken.Minus or KeyToken.Star or KeyToken.CtrlA
            or KeyToken.CtrlG or KeyToken.CtrlU or KeyToken.AltEquals or KeyToken.CtrlO
            or KeyToken.CtrlP or KeyToken.CtrlShiftP or KeyToken.AltO or KeyToken.AltC)
        {
            if (CommandCatalog.Find(pressed) is { Available: true } spec)
            {
                key.Handled = true;
                Invoke(spec.Command);
            }
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

    private void ShowPalette()
    {
        MycCommand? command = CommandPalette.Ask(_app);
        if (command is null)
        {
            return;
        }

        _app.AddTimeout(TimeSpan.Zero, () =>
        {
            Invoke(command.Value);
            return false;
        });
    }

    private void EditFile()
    {
        if (ActivePanel() is not { } panel)
        {
            return;
        }

        FileEntry? entry = panel.CursorEntry;
        if (entry is null || entry.IsParent || entry.IsContainer)
        {
            panel.ShowError("Not a file.");
            return;
        }

        string? handler = EditorLaunch.CurrentHost == EditorHost.Linux ? SystemFileOpener.TextPlainHandler() : null;
        ExternalEdit edit = EditorLaunch.Resolve(
            entry.FullPath,
            _editor(),
            Environment.GetEnvironmentVariable("VISUAL"),
            Environment.GetEnvironmentVariable("EDITOR"),
            EditorLaunch.CurrentHost,
            handler);
        bool tookTerminal = edit is ExternalEdit.Spawn { TakeTerminal: true };
        string? error = edit is ExternalEdit.Spawn { TakeTerminal: true } spawn
            ? TerminalHandoff.RunEditor(_app, spawn)
            : _opener.Start(edit);
        if (error is null)
        {
            panel.ClearError();
            if (tookTerminal)
            {
                panel.Refresh();
            }
        }
        else
        {
            panel.ShowError(error);
        }
    }

    private void OpenShell()
    {
        FilePanelView panel = ActivePanel();
        if (panel.Directory.Length == 0 || !Directory.Exists(panel.Directory))
        {
            panel.Refresh();
            return;
        }

        string shell = ShellLaunch.Resolve(Environment.GetEnvironmentVariable("SHELL"), File.Exists);
        string? error = TerminalHandoff.RunShell(_app, shell, panel.Directory);
        panel.Refresh();
        if (error is not null)
        {
            panel.ShowError(error);
        }
    }

    private bool HasJump => _left.JumpQuery is not null || _right.JumpQuery is not null;

    private void NoteFocus(FilePanelView panel)
    {
        if (!ReferenceEquals(_active, panel))
        {
            ClearJump();
        }

        _active = panel;
    }

    /// <summary>
    /// Letters, digits, <c>.</c>, and <c>_</c> grow a prefix. <c>-</c> grows it only once one has started,
    /// so a lone minus still deselects. A character that matches nothing is ignored.
    /// </summary>
    private bool TryTypeJump(Key key)
    {
        if (key.EventType == KeyEventType.Release)
        {
            return false;
        }

        FilePanelView panel = ActivePanel();
        string current = panel.JumpQuery ?? "";
        if ((key == Key.Backspace || key == Key.Backspace.WithShift) && !key.IsCtrl && !key.IsAlt)
        {
            if (current.Length == 0)
            {
                return false;
            }

            string shorter = current[..^1];
            if (shorter.Length == 0)
            {
                ClearJump();
            }
            else
            {
                panel.ShowJump(shorter);
                ArmJump();
            }

            return true;
        }

        char? typed = JumpCharacter(key, current.Length > 0);
        if (typed is not char next)
        {
            return false;
        }

        if (!panel.AcceptJump(current + next))
        {
            if (current.Length > 0)
            {
                ArmJump();
            }

            return true;
        }

        ArmJump();
        return true;
    }

    private static char? JumpCharacter(Key key, bool extending)
    {
        if (key.IsCtrl || key.IsAlt)
        {
            return null;
        }

        int value = key.AsRune.Value;
        if (value is < 32 or > char.MaxValue)
        {
            return null;
        }

        char character = (char)value;
        if (char.IsLetter(character) || char.IsDigit(character) || character is '.' or '_')
        {
            return character;
        }

        return extending && character == '-' ? character : null;
    }

    private void ArmJump()
    {
        CancelJumpTimer();
        _jumpTimer = _app.AddTimeout(TimeSpan.FromSeconds(2), () =>
        {
            _jumpTimer = null;
            _left.ClearJump();
            _right.ClearJump();
            return false;
        });
    }

    private void ClearJump()
    {
        CancelJumpTimer();
        _left.ClearJump();
        _right.ClearJump();
    }

    private void CancelJumpTimer()
    {
        if (_jumpTimer is null)
        {
            return;
        }

        object timer = _jumpTimer;
        _jumpTimer = null;
        _app.RemoveTimeout(timer);
    }

    private void CopyPath()
    {
        if (CursorText(path: true) is not { } text)
        {
            return;
        }

        CopyText(text);
    }

    private void CopyName()
    {
        if (CursorText(path: false) is not { } text)
        {
            return;
        }

        CopyText(text);
    }

    private string? CursorText(bool path)
    {
        FilePanelView panel = ActivePanel();
        if (panel.CursorEntry is not { } entry || panel.Directory.Length == 0)
        {
            panel.ShowError("Nothing to copy.");
            return null;
        }

        if (path)
        {
            return entry.IsParent ? panel.Directory : entry.FullPath;
        }

        if (!entry.IsParent)
        {
            return entry.Name;
        }

        string directory = panel.Directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string name = Path.GetFileName(directory);
        return name.Length == 0 ? panel.Directory : name;
    }

    private void CopyText(string text)
    {
        FilePanelView panel = ActivePanel();
        if (_app.Clipboard is not { IsSupported: true } clipboard || !clipboard.TrySetClipboardData(text))
        {
            panel.ShowError("Couldn't copy that.");
            return;
        }

        panel.ClearError();
    }

    private void NewFile()
    {
        if (ActivePanel() is not { } panel)
        {
            return;
        }

        string typed = "";
        string? error = null;
        while (true)
        {
            NameAnswer answer = NamePrompt.Ask(_app, "New file", "New file name:", "Create", typed, 0, error);
            if (answer.Text is null)
            {
                return;
            }

            FileChange change = _files.CreateFile(panel.Directory, answer.Text);
            if (change.Succeeded)
            {
                RefreshAffected(panel, change.SelectedName, null, null);
                return;
            }

            typed = answer.Text;
            error = change.Error;
        }
    }

    private void ViewFile()
    {
        if (ActivePanel() is not { } panel)
        {
            return;
        }

        FileEntry? entry = panel.CursorEntry;
        if (entry is null || entry.IsParent || entry.IsContainer)
        {
            panel.ShowError("Not a file.");
            return;
        }

        if (Interlocked.CompareExchange(ref _viewOpening, 1, 0) != 0)
        {
            return;
        }

        string path = entry.FullPath;
        string name = entry.Name;
        bool markdown = ViewerText.LooksLikeMarkdown(name);
        _ = Task.Run(() => ViewerText.Read(path)).ContinueWith(done =>
        {
            try
            {
                _app.Invoke(() =>
                {
                    try
                    {
                        if (done.IsFaulted)
                        {
                            panel.ShowError(done.Exception?.GetBaseException().Message ?? "Could not read the file.");
                            return;
                        }

                        ViewerText.Content content = done.Result;
                        if (!content.Ok)
                        {
                            panel.ShowError(content.Error ?? "Could not read the file.");
                            return;
                        }

                        panel.ClearError();
                        FileViewer.Show(_app, name, content.Text, markdown);
                    }
                    finally
                    {
                        _viewOpening = 0;
                    }
                });
            }
            catch (ObjectDisposedException)
            {
                _viewOpening = 0;
            }
        }, TaskScheduler.Default);
    }

    private static bool IsPlainLetter(Key key, char lower)
    {
        if (key.IsCtrl || key.IsAlt)
        {
            return false;
        }

        int rune = key.AsRune.Value;
        return rune == lower || rune == char.ToUpperInvariant(lower);
    }

    private void GoToPath()
    {
        FilePanelView panel = ActivePanel();
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        GoToAnswer? answer = GoToPrompt.Ask(_app, _files, panel.Directory, home, panel.ShowHidden, _gotoHistory);
        if (answer is not { } chosen)
        {
            return;
        }

        panel.Open(chosen.Directory, chosen.SelectName);
        RememberPath(chosen.Directory);
    }

    private void SameDirectory()
    {
        FilePanelView active = ActivePanel();
        FilePanelView other = ReferenceEquals(active, _left) ? _right : _left;
        string? name = active.CursorEntry is { IsParent: false } entry ? entry.Name : null;
        other.Open(active.Directory, name);
    }

    private void SwapPanels()
    {
        string leftDirectory = _left.Directory;
        string rightDirectory = _right.Directory;
        string? leftName = _left.CursorEntry?.Name;
        string? rightName = _right.CursorEntry?.Name;
        _left.Open(rightDirectory, rightName);
        _right.Open(leftDirectory, leftName);
    }

    private void RememberPath(string directory)
    {
        _gotoHistory.RemoveAll(path => string.Equals(path, directory, StringComparison.OrdinalIgnoreCase));
        _gotoHistory.Insert(0, directory);
        if (_gotoHistory.Count > 16)
        {
            _gotoHistory.RemoveAt(_gotoHistory.Count - 1);
        }
    }

    private void SelectByPattern(bool add)
    {
        if (ActivePanel() is not { } panel)
        {
            return;
        }

        string? pattern = PatternPrompt.Ask(
            _app,
            add ? "Select" : "Deselect",
            add ? "Select" : "Deselect",
            panel.Entries);
        if (pattern is null)
        {
            return;
        }

        if (add)
        {
            panel.SelectMatching(pattern);
        }
        else
        {
            panel.DeselectMatching(pattern);
        }
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

        string? destination = _confirm().Transfer
            ? TransferPrompt.Ask(_app, copy ? "Copy" : "Move", selection.Length, Other(panel).Directory)
            : Other(panel).Directory;
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

        // Shift+F8 always asks. Trash asks only while Confirm before → Delete is on.
        if ((permanent || _confirm().Delete)
            && !DeletePrompt.Ask(_app, permanent, selection.Select(entry => entry.Name).ToArray()))
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

    private FilePanelView ActivePanel()
    {
        if (_left.HasFocus)
        {
            _active = _left;
        }
        else if (_right.HasFocus)
        {
            _active = _right;
        }

        return _active;
    }
}
