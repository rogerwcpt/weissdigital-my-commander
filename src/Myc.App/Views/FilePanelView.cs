using System.Text;
using Myc.App.Theming;
using Myc.Core.Display;
using Myc.Core.FileSystem;
using Myc.Core.Platform;
using Myc.Core.Selection;
using Myc.Core.Sorting;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;

namespace Myc.App.Views;

/// <summary>
/// One directory panel. The cursor and the mark set are independent, Space toggles a mark
/// and moves down, and only the visible rows are drawn.
/// </summary>
public sealed class FilePanelView : View
{
    private readonly IFileSystem _files;
    private readonly IFileOpener _opener;
    private readonly HashSet<string> _marks = new(FileNameComparer.Ordinal);
    private IReadOnlyList<FileEntry> _entries = [];
    private string _directory = "";
    private bool _showHidden = true;
    private string _sort = PanelSort.Name;
    private bool _descending;
    private string? _error;
    private int _cursor;
    private int _scroll;

    public FilePanelView(IFileSystem files, IFileOpener opener)
    {
        _files = files;
        _opener = opener;
        CanFocus = true;
        BorderStyle = LineStyle.Rounded;
        SetScheme(Graphite.Inactive);
    }

    /// <summary>Raised when this panel takes the keyboard, so a command can find it after the menu closes.</summary>
    public event Action<FilePanelView>? BecameFocused;

    protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView)
    {
        SetScheme(newHasFocus ? Graphite.Scheme : Graphite.Inactive);
        SetNeedsDraw();
        if (newHasFocus)
        {
            BecameFocused?.Invoke(this);
        }

        base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);
    }

    /// <summary>The border scheme follows focus, and a theme change has to say so again.</summary>
    public void Restyle()
    {
        SetScheme(HasFocus ? Graphite.Scheme : Graphite.Inactive);
        SetNeedsDraw();
    }

    /// <summary>Raised when the panel is showing a different directory, so a watcher can follow it.</summary>
    public event Action? DirectoryChanged;

    public string Directory => _directory;

    /// <summary>Dotfiles stay in the list until Alt+. or the Panel menu hides them.</summary>
    public bool ShowHidden
    {
        get => _showHidden;
        set
        {
            if (_showHidden == value)
            {
                return;
            }

            _showHidden = value;
            if (_directory.Length > 0)
            {
                Apply(_directory, CursorEntry?.Name, stayOnError: true);
            }
        }
    }

    public string Sort => _sort;

    public bool SortDescending => _descending;

    /// <summary>Same order again reverses it. A different order starts at the top of that column.</summary>
    public void SortBy(string sort)
    {
        string next = PanelSort.Normalize(sort);
        UseSort(next, _sort == next ? !_descending : false);
    }

    public void UseSort(string sort, bool descending)
    {
        string next = PanelSort.Normalize(sort);
        if (_sort == next && _descending == descending)
        {
            return;
        }

        _sort = next;
        _descending = descending;
        if (_directory.Length > 0)
        {
            Apply(_directory, CursorEntry?.Name, stayOnError: true);
        }
    }

    public void ToggleHidden() => ShowHidden = !_showHidden;

    public IReadOnlyList<FileEntry> Entries => _entries;

    public IReadOnlySet<string> Marks => _marks;

    public int CursorIndex => _cursor;

    public FileEntry? CursorEntry => _cursor >= 0 && _cursor < _entries.Count ? _entries[_cursor] : null;

    public void Open(string directory, string? selectName = null) => Apply(directory, selectName, stayOnError: false);

    public void Refresh() => Reload(CursorEntry?.Name);

    public void Reload(string? selectName) => Apply(_directory, selectName, stayOnError: true);

    /// <summary>Reloads this folder. If the cursor's name is gone, the same row stays selected.</summary>
    public void ReloadKeepingCursor()
    {
        string? name = CursorEntry?.Name;
        int index = _cursor;
        Apply(_directory, name, stayOnError: true);
        if (name is not null && IndexOf(name) < 0 && _entries.Count > 0)
        {
            _cursor = Math.Clamp(index, 0, _entries.Count - 1);
            KeepCursorVisible(EntryHeight());
            SetNeedsDraw();
        }
    }

    public void ShowError(string message)
    {
        _error = message;
        SetNeedsDraw();
    }

    public void ReplaceMark(string oldName, string newName)
    {
        if (!_marks.Remove(oldName))
        {
            return;
        }

        _marks.Add(newName);
    }

    public void ClearMarks()
    {
        if (_marks.Count == 0)
        {
            return;
        }

        _marks.Clear();
        SetNeedsDraw();
    }

    /// <summary>Enter on the cursor: open the directory, or the file in the default app.</summary>
    public void ActivateSelection() => Activate();

    /// <summary>Shows the cursor item in Finder. <c>..</c> reveals the folder this panel is showing.</summary>
    public void RevealCursor()
    {
        if (CursorEntry is not { } entry || _directory.Length == 0)
        {
            return;
        }

        string path = entry.IsParent ? _directory : entry.FullPath;
        _error = _opener.Reveal(path);
        SetNeedsDraw();
    }

    /// <summary>Space: toggle the mark on the cursor and move down.</summary>
    public void ToggleMark() => ToggleMarkAndAdvance();

    /// <summary>Adds every visible name the pattern matches. Existing marks stay. <c>..</c> is never marked.</summary>
    public void SelectMatching(string pattern) => ChangeMarks(pattern, add: true);

    /// <summary>Removes marks whose names match the pattern. Other marks stay.</summary>
    public void DeselectMatching(string pattern) => ChangeMarks(pattern, add: false);

    /// <summary>Marks every visible row except <c>..</c>.</summary>
    public void SelectAll()
    {
        bool changed = false;
        foreach (FileEntry entry in _entries)
        {
            if (!entry.IsParent && _marks.Add(entry.Name))
            {
                changed = true;
            }
        }

        if (changed)
        {
            SetNeedsDraw();
        }
    }

    /// <summary>Marked names become unmarked, and the other way around. <c>..</c> stays unmarked.</summary>
    public void InvertMarks()
    {
        foreach (FileEntry entry in _entries)
        {
            if (entry.IsParent)
            {
                continue;
            }

            if (!_marks.Remove(entry.Name))
            {
                _marks.Add(entry.Name);
            }
        }

        SetNeedsDraw();
    }

    private void ChangeMarks(string pattern, bool add)
    {
        bool changed = false;
        foreach (FileEntry entry in _entries)
        {
            if (entry.IsParent || !NamePattern.Matches(pattern, entry.Name))
            {
                continue;
            }

            bool updated = add ? _marks.Add(entry.Name) : _marks.Remove(entry.Name);
            changed |= updated;
        }

        if (changed)
        {
            SetNeedsDraw();
        }
    }

    protected override bool OnKeyDown(Key key)
    {
        int page = Math.Max(1, EntryHeight());
        bool handled = key.NoShift.NoCtrl.NoAlt switch
        {
            var plain when plain == Key.CursorUp => Move(-1),
            var plain when plain == Key.CursorDown => Move(1),
            var plain when plain == Key.PageUp => Move(-page),
            var plain when plain == Key.PageDown => Move(page),
            var plain when plain == Key.Home => MoveTo(0),
            var plain when plain == Key.End => MoveTo(_entries.Count - 1),
            var plain when plain == Key.Space || plain == Key.InsertChar => ToggleMarkAndAdvance(),
            var plain when plain == Key.CursorRight => EnterCursor(),
            var plain when plain == Key.CursorLeft => GoUp(),
            var plain when plain == Key.Enter => Activate(),
            _ => false,
        };

        return handled || base.OnKeyDown(key);
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        bool doubleClick = mouse.Flags.HasFlag(MouseFlags.LeftButtonDoubleClicked);
        if ((!mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked) && !doubleClick) || mouse.Position is not { } position)
        {
            return base.OnMouseEvent(mouse);
        }

        int row = position.Y;
        if (row <= 0 || row >= Viewport.Height - 2)
        {
            return true;
        }

        MoveTo(_scroll + row - 1);
        SetFocus();
        if (doubleClick)
        {
            Activate();
        }

        return true;
    }

    private bool EnterCursor()
    {
        if (CursorEntry is not { } entry)
        {
            return true;
        }

        if (DirectoryNavigation.Enter(entry, _directory) is { } move)
        {
            Apply(move.Directory, move.SelectName, stayOnError: true);
        }

        return true;
    }

    private bool GoUp()
    {
        if (DirectoryNavigation.Up(_directory) is { } move)
        {
            Apply(move.Directory, move.SelectName, stayOnError: true);
        }

        return true;
    }

    private bool Activate()
    {
        if (CursorEntry is not { } entry)
        {
            return true;
        }

        if (DirectoryNavigation.Enter(entry, _directory) is { } move)
        {
            Apply(move.Directory, move.SelectName, stayOnError: true);
            return true;
        }

        _error = _opener.Open(entry.FullPath);
        SetNeedsDraw();
        return true;
    }

    private void Apply(string directory, string? selectName, bool stayOnError)
    {
        DirectoryListing listing = _files.List(directory, _showHidden);
        if (listing.Error is not null && stayOnError)
        {
            _error = listing.Error;
            SetNeedsDraw();
            return;
        }

        bool sameDirectory = string.Equals(_directory, listing.Directory, StringComparison.Ordinal);
        _directory = listing.Directory;
        _entries = EntrySorter.Sort(listing.Entries, _sort, _descending);
        _error = listing.Error;
        if (!sameDirectory)
        {
            _marks.Clear();
            _scroll = 0;
        }

        _marks.RemoveWhere(name => _entries.All(entry => !EntryNames.Same(entry.Name, name)));

        int selected = selectName is null ? -1 : IndexOf(selectName);
        _cursor = selected >= 0 ? selected : 0;
        KeepCursorVisible(EntryHeight());
        Title = DisplayPath(_directory);
        SetNeedsDraw();
        if (!sameDirectory)
        {
            DirectoryChanged?.Invoke();
        }
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        int height = Math.Max(0, Viewport.Height);
        int width = Math.Max(0, Viewport.Width);
        if (height == 0 || width == 0)
        {
            return true;
        }

        if (height < 3)
        {
            SetAttribute(Graphite.Text);
            AddStr(0, 0, Fit(_error ?? DisplayPath(_directory), width));
            return true;
        }

        PanelColumns columns = PanelColumns.For(width);
        int entryRows = EntryHeight(height);
        KeepCursorVisible(entryRows);

        DrawHeader(columns, width);
        for (int row = 0; row < entryRows; row++)
        {
            int index = _scroll + row;
            if (index >= _entries.Count)
            {
                DrawBlank(row + 1, width);
                continue;
            }

            FileEntry entry = _entries[index];
            bool marked = _marks.Contains(entry.Name);
            bool cursor = index == _cursor;
            SetAttribute(RowColor(entry, marked, cursor));
            AddStr(0, row + 1, Fit(FormatRow(entry, marked, columns), width));
        }

        SetAttribute(Graphite.Rule);
        AddStr(0, height - 2, new string('─', width));
        SetAttribute(_error is null ? Graphite.Text : Graphite.Marked);
        AddStr(0, height - 1, Fit(Footer(), width));
        return true;
    }

    private bool ToggleMarkAndAdvance()
    {
        if (_entries.Count == 0)
        {
            return true;
        }

        FileEntry entry = _entries[_cursor];
        if (!entry.IsParent)
        {
            if (!_marks.Remove(entry.Name))
            {
                _marks.Add(entry.Name);
            }
        }

        Move(1);
        return true;
    }

    private bool Move(int delta) => MoveTo(_cursor + delta);

    private bool MoveTo(int index)
    {
        if (_entries.Count == 0)
        {
            return true;
        }

        _cursor = Math.Clamp(index, 0, _entries.Count - 1);
        KeepCursorVisible(EntryHeight());
        SetNeedsDraw();
        return true;
    }

    private void KeepCursorVisible(int entryRows)
    {
        if (entryRows <= 0)
        {
            return;
        }

        if (_cursor < _scroll)
        {
            _scroll = _cursor;
        }
        else if (_cursor >= _scroll + entryRows)
        {
            _scroll = _cursor - entryRows + 1;
        }
    }

    private int EntryHeight() => EntryHeight(Math.Max(0, Viewport.Height));

    private static int EntryHeight(int height) => Math.Max(0, height - 3);

    private Terminal.Gui.Drawing.Attribute RowColor(FileEntry entry, bool marked, bool cursor)
    {
        if (cursor && HasFocus)
        {
            return marked ? Graphite.CursorMarked : Graphite.Cursor;
        }

        if (cursor)
        {
            return Graphite.InactiveCursor;
        }

        if (marked)
        {
            return Graphite.Marked;
        }

        if (entry.IsHidden)
        {
            return entry.IsContainer ? Graphite.HiddenDirectory : Graphite.Hidden;
        }

        return entry.IsContainer ? Graphite.Directory : Graphite.Text;
    }

    private void DrawHeader(PanelColumns columns, int width)
    {
        SetAttribute(Graphite.Header);
        AddStr(0, 0, Fit(FormatHeader(columns), width));
    }

    private void DrawBlank(int row, int width)
    {
        SetAttribute(Graphite.Text);
        AddStr(0, row, new string(' ', width));
    }

    private string Footer()
    {
        if (_error is not null)
        {
            return _error;
        }

        List<FileEntry> marked = _entries.Where(entry => _marks.Contains(entry.Name)).ToList();
        if (marked.Count > 0)
        {
            long sum = 0;
            bool anySize = false;
            foreach (FileEntry entry in marked)
            {
                if (entry.Size is long size)
                {
                    sum += size;
                    anySize = true;
                }
            }

            return anySize ? $"{marked.Count} marked · {EntryText.FormatSize(sum)}" : $"{marked.Count} marked";
        }

        FileEntry? cursor = CursorEntry;
        if (cursor is null || cursor.IsParent)
        {
            return "";
        }

        string detail = cursor.Name;
        if (cursor.Kind == FileKind.Symlink && cursor.SymlinkTarget is not null)
        {
            detail += " → " + cursor.SymlinkTarget;
        }
        else if (cursor.Size is long size)
        {
            detail += "  " + EntryText.FormatSize(size);
        }
        else if (cursor.Kind == FileKind.Directory)
        {
            detail += "  <DIR>";
        }

        if (cursor.Modified is DateTimeOffset modified)
        {
            detail += "  " + EntryText.FormatDetailDate(modified.ToLocalTime());
        }

        return detail;
    }

    private string FormatHeader(PanelColumns columns)
    {
        ColumnHeaders headers = PanelSort.Headers(_sort, _descending, columns.ShowSize, columns.ShowModified);
        var line = new StringBuilder();
        line.Append(Pad(headers.Name, columns.Name, right: false));
        if (columns.ShowSize)
        {
            line.Append(' ');
            line.Append(Pad(headers.Size, PanelColumns.SizeWidth, right: true));
        }

        if (columns.ShowModified)
        {
            line.Append(' ');
            line.Append(Pad(headers.Modified, PanelColumns.DateWidth, right: true));
        }

        return line.ToString();
    }

    private static string FormatRow(FileEntry entry, bool marked, PanelColumns columns)
    {
        string prefix = marked
            ? entry.IsContainer && !entry.IsParent ? "• ▸ " : "•   "
            : entry.IsContainer && !entry.IsParent ? "  ▸ " : "    ";
        string name = entry.IsParent ? ".." : entry.IsContainer ? entry.Name + "/" : entry.Name;
        int prefixColumns = prefix.GetColumns();
        int nameBudget = Math.Max(0, columns.Name - prefixColumns);
        string shown = prefix + EntryText.EllipsizeMiddle(name, nameBudget, text => text.GetColumns());

        var line = new StringBuilder();
        line.Append(Pad(shown, columns.Name, right: false));
        if (columns.ShowSize)
        {
            string size = entry.IsParent ? "" : entry.IsContainer ? "<DIR>" : entry.Size is long bytes ? EntryText.FormatSize(bytes) : "";
            line.Append(' ');
            line.Append(Pad(size, PanelColumns.SizeWidth, right: true));
        }

        if (columns.ShowModified)
        {
            string date = entry.Modified is DateTimeOffset modified ? EntryText.FormatListDate(modified.ToLocalTime()) : "";
            line.Append(' ');
            line.Append(Pad(date, PanelColumns.DateWidth, right: true));
        }

        return line.ToString();
    }

    private static string Pad(string text, int columns, bool right)
    {
        int used = text.GetColumns();
        if (used > columns)
        {
            text = EntryText.EllipsizeMiddle(text, columns, value => value.GetColumns());
            used = text.GetColumns();
        }

        int gap = Math.Max(0, columns - used);
        return right ? new string(' ', gap) + text : text + new string(' ', gap);
    }

    private static string Fit(string text, int width)
    {
        string clipped = EntryText.EllipsizeMiddle(text, width, value => value.GetColumns());
        int gap = width - clipped.GetColumns();
        return gap > 0 ? clipped + new string(' ', gap) : clipped;
    }

    private int IndexOf(string name)
    {
        for (int index = 0; index < _entries.Count; index++)
        {
            if (EntryNames.Same(_entries[index].Name, name))
            {
                return index;
            }
        }

        return -1;
    }

    private static string DisplayPath(string path)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.Equals(path, home, StringComparison.Ordinal))
        {
            return "~";
        }

        string prefix = home.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.Ordinal) ? "~" + path[home.Length..] : path;
    }

}
