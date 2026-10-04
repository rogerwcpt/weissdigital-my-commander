using System.Text;
using Myc.App.Theming;
using Myc.Core.Display;
using Myc.Core.FileSystem;
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
    private const int SizeColumn = 7;
    private const int DateColumn = 8;

    private readonly IFileSystem _files;
    private readonly HashSet<string> _marks = [];
    private IReadOnlyList<FileEntry> _entries = [];
    private string _directory = "";
    private string? _error;
    private int _cursor;
    private int _scroll;

    public FilePanelView(IFileSystem files)
    {
        _files = files;
        CanFocus = true;
        BorderStyle = LineStyle.Rounded;
        SetScheme(Graphite.Inactive);
    }

    protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView)
    {
        SetScheme(newHasFocus ? Graphite.Scheme : Graphite.Inactive);
        base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);
    }

    public string Directory => _directory;

    public IReadOnlyList<FileEntry> Entries => _entries;

    public IReadOnlySet<string> Marks => _marks;

    public int CursorIndex => _cursor;

    public FileEntry? CursorEntry => _cursor >= 0 && _cursor < _entries.Count ? _entries[_cursor] : null;

    public void Open(string directory)
    {
        string? keep = CursorEntry?.Name;
        bool sameDirectory = string.Equals(_directory, Path.GetFullPath(directory), StringComparison.Ordinal);
        DirectoryListing listing = _files.List(directory, showHidden: false);
        _directory = listing.Directory;
        _entries = listing.Entries;
        _error = listing.Error;
        _marks.RemoveWhere(name => _entries.All(entry => entry.Name != name));

        int kept = keep is null ? -1 : IndexOf(keep);
        _cursor = kept >= 0 ? kept : 0;
        if (!sameDirectory)
        {
            _scroll = 0;
        }

        Title = DisplayPath(_directory);
        SetNeedsDraw();
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
            _ => false,
        };

        return handled || base.OnKeyDown(key);
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (!mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked) || mouse.Position is not { } position)
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
        return true;
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

        ColumnPlan columns = ColumnPlan.For(width);
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

        return entry.IsContainer ? Graphite.Directory : Graphite.Text;
    }

    private void DrawHeader(ColumnPlan columns, int width)
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

    private static string FormatHeader(ColumnPlan columns)
    {
        var line = new StringBuilder();
        line.Append(Pad("Name", columns.Name, right: false));
        if (columns.ShowSize)
        {
            line.Append(' ');
            line.Append(Pad("Size", SizeColumn, right: true));
        }

        if (columns.ShowModified)
        {
            line.Append(' ');
            line.Append(Pad("Modified", DateColumn, right: true));
        }

        return line.ToString();
    }

    private static string FormatRow(FileEntry entry, bool marked, ColumnPlan columns)
    {
        string prefix = marked
            ? entry.IsContainer && !entry.IsParent ? "•▸" : "• "
            : entry.IsContainer && !entry.IsParent ? " ▸" : "  ";
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
            line.Append(Pad(size, SizeColumn, right: true));
        }

        if (columns.ShowModified)
        {
            string date = entry.Modified is DateTimeOffset modified ? EntryText.FormatListDate(modified.ToLocalTime()) : "";
            line.Append(' ');
            line.Append(Pad(date, DateColumn, right: true));
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
            if (_entries[index].Name == name)
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

    private readonly record struct ColumnPlan(int Name, bool ShowSize, bool ShowModified)
    {
        public static ColumnPlan For(int width)
        {
            int remaining = width;
            bool showModified = remaining >= 20 + DateColumn;
            if (showModified)
            {
                remaining -= DateColumn + 1;
            }

            bool showSize = remaining >= 16 + SizeColumn;
            if (showSize)
            {
                remaining -= SizeColumn + 1;
            }

            return new ColumnPlan(Math.Max(0, remaining), showSize, showModified);
        }
    }
}
