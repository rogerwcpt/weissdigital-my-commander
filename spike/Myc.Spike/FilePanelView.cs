using System.Diagnostics;
using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;

namespace Myc.Spike;

/// <summary>
/// Norton-style panel: the cursor and the mark set are independent, Space toggles
/// the mark and moves down, and only the visible rows are drawn.
/// </summary>
public sealed class FilePanelView : View
{
    private readonly IReadOnlyList<SpikeEntry> _entries;
    private readonly HashSet<int> _marks = [];
    private int _cursor;
    private int _scroll;

    public FilePanelView(IReadOnlyList<SpikeEntry> entries)
    {
        _entries = entries;
        CanFocus = true;
        BorderStyle = LineStyle.Rounded;
        Title = "custom";
    }

    public int MarkCount => _marks.Count;

    public long LastDrawMicroseconds { get; private set; }

    public int LastRowsDrawn { get; private set; }

    public event Action? Changed;

    public void RefreshTitle()
    {
        Title = $"custom  {_marks.Count} marked  paint {LastDrawMicroseconds}µs  {LastRowsDrawn} rows";
    }

    protected override bool OnKeyDown(Key key)
    {
        int page = Math.Max(1, Viewport.Height);
        bool handled = key.NoShift.NoCtrl.NoAlt switch
        {
            var k when k == Key.CursorUp => Move(-1),
            var k when k == Key.CursorDown => Move(1),
            var k when k == Key.PageUp => Move(-page),
            var k when k == Key.PageDown => Move(page),
            var k when k == Key.Home => MoveTo(0),
            var k when k == Key.End => MoveTo(_entries.Count - 1),
            var k when k == Key.Space => ToggleMarkAndAdvance(),
            _ => false,
        };

        return handled || base.OnKeyDown(key);
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        long start = Stopwatch.GetTimestamp();
        int height = Math.Max(0, Viewport.Height);
        int width = Math.Max(0, Viewport.Width);
        KeepCursorVisible(height);

        int drawn = 0;
        for (int row = 0; row < height; row++)
        {
            int index = _scroll + row;
            if (index >= _entries.Count)
            {
                SetAttributeForRole(VisualRole.Normal);
                AddStr(0, row, new string(' ', width));
                continue;
            }

            bool marked = _marks.Contains(index);
            bool cursor = index == _cursor && HasFocus;
            SetAttributeForRole(cursor ? VisualRole.Focus : marked ? VisualRole.Highlight : VisualRole.Normal);
            AddStr(0, row, FormatRow(_entries[index], marked, width));
            drawn++;
        }

        LastRowsDrawn = drawn;
        LastDrawMicroseconds = (Stopwatch.GetTimestamp() - start) * 1_000_000 / Stopwatch.Frequency;
        return true;
    }

    /// <summary>Formats one row to exactly <paramref name="columns"/> terminal columns.</summary>
    public static string FormatRow(SpikeEntry entry, bool marked, int columns)
    {
        string body = $"{(marked ? "•" : " ")} {(entry.IsDirectory ? "▸" : " ")} {entry.Name}";
        return Fit(body, columns);
    }

    public static string Fit(string text, int columns)
    {
        if (columns <= 0)
        {
            return "";
        }

        var builder = new StringBuilder();
        int used = 0;
        foreach (Rune rune in text.EnumerateRunes())
        {
            int width = rune.GetColumns();
            if (used + width > columns)
            {
                break;
            }

            builder.Append(rune.ToString());
            used += width;
        }

        if (used < columns)
        {
            builder.Append(' ', columns - used);
        }

        return builder.ToString();
    }

    private bool ToggleMarkAndAdvance()
    {
        if (_entries.Count == 0 || _entries[_cursor].Name == "..")
        {
            return true;
        }

        if (!_marks.Remove(_cursor))
        {
            _marks.Add(_cursor);
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
        KeepCursorVisible(Math.Max(1, Viewport.Height));
        SetNeedsDraw();
        Changed?.Invoke();
        return true;
    }

    private void KeepCursorVisible(int height)
    {
        if (height <= 0)
        {
            return;
        }

        if (_cursor < _scroll)
        {
            _scroll = _cursor;
        }
        else if (_cursor >= _scroll + height)
        {
            _scroll = _cursor - height + 1;
        }
    }
}
