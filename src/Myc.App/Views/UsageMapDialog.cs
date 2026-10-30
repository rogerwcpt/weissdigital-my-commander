using Myc.App.Theming;
using Myc.Core.Display;
using Myc.Core.FileSystem;
using Myc.Core.Platform;
using Myc.Core.Selection;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Views;

/// <summary>
/// Full-screen size map of one folder. Esc closes it. Enter opens a directory, Left goes up.
/// </summary>
internal sealed class UsageMapDialog : Window
{
    private const string OtherCursor = "\0other";

    private readonly IApplication _app;
    private readonly IFileOpener _opener;
    private readonly Action<string, FolderMeasureResult> _measuredCallback;
    private readonly bool _showHidden;
    private readonly object _gate = new();
    private string _directory;
    private FolderMeasureResult? _measured;
    private string? _measuredDirectory;
    private CancellationTokenSource? _scan;
    private int _generation;
    private int _seen;
    private int _scanFiles;
    private string? _scanNote;
    private string? _error;
    private string? _cursorName;
    private ScanDelivery? _delivery;
    private object? _pump;
    private bool _closed;

    private UsageMapDialog(
        IApplication app,
        string directory,
        bool showHidden,
        IFileOpener opener,
        Action<string, FolderMeasureResult> measuredCallback)
    {
        _app = app;
        _directory = directory;
        _showHidden = showHidden;
        _opener = opener;
        _measuredCallback = measuredCallback;
        CanFocus = true;
        SetScheme(Graphite.Scheme);
        UpdateTitle();
        Initialized += (_, _) => SetFocus();
        // Registered on the UI thread before the dialog loop waits, so a finished scan
        // is applied even when nothing else wakes the terminal.
        _pump = _app.AddTimeout(TimeSpan.FromMilliseconds(50), Pump);
        StartScan();
    }

    public static void Show(
        IApplication app,
        string directory,
        bool showHidden,
        IFileOpener opener,
        Action<string, FolderMeasureResult> measuredCallback)
    {
        using var dialog = new UsageMapDialog(app, directory, showHidden, opener, measuredCallback);
        app.Run(dialog);
    }

    /// <summary>Arrow keys move. Enter opens. Left goes to the parent. Esc is left to the caller.</summary>
    public bool HandleKey(Key key)
    {
        return key.NoShift.NoCtrl.NoAlt switch
        {
            var plain when plain == Key.CursorUp => MoveCursor(0, -1),
            var plain when plain == Key.CursorDown => MoveCursor(0, 1),
            var plain when plain == Key.CursorLeft => GoUp(),
            var plain when plain == Key.CursorRight => OpenCurrent(openFile: false),
            var plain when plain == Key.Enter => OpenCurrent(openFile: true),
            var plain when plain == Key.Home => MoveEnd(first: true),
            var plain when plain == Key.End => MoveEnd(first: false),
            var plain when plain == Key.PageUp => MoveCursor(0, -1),
            var plain when plain == Key.PageDown => MoveCursor(0, 1),
            _ => false,
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _closed = true;
            if (_pump is not null)
            {
                _app.RemoveTimeout(_pump);
                _pump = null;
            }

            StopScan();
        }

        base.Dispose(disposing);
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        bool doubleClick = mouse.Flags.HasFlag(MouseFlags.LeftButtonDoubleClicked);
        if ((!mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked) && !doubleClick) || mouse.Position is not { } position)
        {
            return base.OnMouseEvent(mouse);
        }

        int width = Math.Max(0, Viewport.Width);
        int height = MapHeight(Math.Max(0, Viewport.Height));
        if (TryLayout(width, height, out List<MapItem> items, out IReadOnlyList<MapBlock> blocks))
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                MapBlock block = blocks[i];
                if (position.X < block.X || position.X >= block.X + block.Width
                    || position.Y < block.Y || position.Y >= block.Y + block.Height)
                {
                    continue;
                }

                Remember(items[i]);
                SetNeedsDraw();
                if (doubleClick)
                {
                    OpenCurrent(openFile: true);
                }

                return true;
            }
        }

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

        DrawMap(width, height);
        return true;
    }

    private void OpenDirectory(string directory, string? selectName)
    {
        _directory = directory;
        _cursorName = selectName;
        _measured = null;
        _measuredDirectory = null;
        _scanNote = null;
        _error = null;
        UpdateTitle();
        StartScan();
        SetNeedsDraw();
    }

    private void UpdateTitle() => Title = "Usage map — " + DisplayPath(_directory);

    private void StartScan()
    {
        if (_directory.Length == 0 || _closed)
        {
            return;
        }

        CancellationTokenSource? previous = _scan;
        var scan = new CancellationTokenSource();
        _scan = scan;
        _scanFiles = 0;
        Volatile.Write(ref _seen, 0);
        _scanNote = null;
        int generation;
        lock (_gate)
        {
            generation = ++_generation;
            _delivery = null;
        }

        previous?.Cancel();
        string directory = _directory;
        Task.Run(() =>
        {
            try
            {
                FolderMeasureResult result = FolderMeasure.Scan(
                    directory,
                    scan.Token,
                    seen => Volatile.Write(ref _seen, seen));
                Deliver(generation, directory, result, cancelled: false);
            }
            catch (OperationCanceledException)
            {
                Deliver(generation, directory, null, cancelled: true);
            }
            catch (Exception)
            {
                Deliver(generation, directory, null, cancelled: false);
            }
            finally
            {
                scan.Dispose();
            }
        });
    }

    private void Deliver(int generation, string directory, FolderMeasureResult? result, bool cancelled)
    {
        lock (_gate)
        {
            if (generation != _generation)
            {
                return;
            }

            _delivery = new ScanDelivery(generation, directory, result, cancelled);
        }
    }

    /// <summary>Applies a finished scan. The dialog loop is asleep until this timer wakes it.</summary>
    private bool Pump()
    {
        if (_closed)
        {
            return false;
        }

        bool dirty = false;
        int seen = Volatile.Read(ref _seen);
        if (_scan is not null && seen != _scanFiles)
        {
            _scanFiles = seen;
            dirty = true;
        }

        ScanDelivery? delivery;
        lock (_gate)
        {
            delivery = _delivery;
            _delivery = null;
        }

        if (delivery is not null && delivery.Generation == _generation)
        {
            _scan = null;
            if (delivery.Cancelled)
            {
                _scanNote = "Scan cancelled.";
            }
            else if (delivery.Result is { } measured)
            {
                _measured = measured;
                _measuredDirectory = delivery.Directory;
                _scanNote = null;
                _measuredCallback(delivery.Directory, measured);
            }
            else
            {
                _scanNote = "Could not measure this folder.";
            }

            dirty = true;
        }

        if (dirty)
        {
            SetNeedsDraw();
        }

        return true;
    }

    private void StopScan()
    {
        if (_scan is not { } scan)
        {
            return;
        }

        _scan = null;
        try
        {
            scan.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void DrawMap(int width, int height)
    {
        int mapHeight = MapHeight(height);
        SetAttribute(Graphite.Text);
        for (int y = 0; y < mapHeight; y++)
        {
            AddStr(0, y, new string(' ', width));
        }

        if (TryLayout(width, mapHeight, out List<MapItem> items, out IReadOnlyList<MapBlock> blocks))
        {
            int cursor = IndexOf(items);
            for (int i = 0; i < items.Count; i++)
            {
                if (i == cursor || blocks[i].Width <= 0 || blocks[i].Height <= 0)
                {
                    continue;
                }

                PaintBlock(items[i], blocks[i], cursor: false, i);
            }

            if (cursor < blocks.Count && blocks[cursor].Width > 0 && blocks[cursor].Height > 0)
            {
                PaintBlock(items[cursor], blocks[cursor], cursor: true, cursor);
            }
        }
        else
        {
            SetAttribute(Graphite.Text);
            AddStr(0, 0, Fit(Placeholder(), width));
        }

        SetAttribute(Graphite.Rule);
        AddStr(0, height - 2, new string('─', width));
        SetAttribute(_error is null ? Graphite.Text : Graphite.Marked);
        AddStr(0, height - 1, Fit(Footer(), width));
    }

    private string Placeholder()
    {
        if (_scan is not null)
        {
            return _scanFiles > 0 ? $"Measuring… {_scanFiles}" : "Measuring…";
        }

        if (_scanNote is not null)
        {
            return _scanNote;
        }

        if (_measured is { Skipped: > 0 })
        {
            return "Nothing readable in this folder.";
        }

        return "Empty";
    }

    private string Footer()
    {
        if (_error is not null)
        {
            return _error + "   Esc close";
        }

        int width = Math.Max(0, Viewport.Width);
        int height = MapHeight(Math.Max(0, Viewport.Height));
        if (!TryLayout(width, height, out List<MapItem> items, out _))
        {
            string waiting = _scan is not null ? Placeholder() : _scanNote ?? "";
            return waiting.Length == 0 ? "Esc close" : waiting + "   Esc close";
        }

        MapItem item = items[IndexOf(items)];
        long shown = 0;
        foreach (MapItem candidate in items)
        {
            if (!candidate.OtherVolume)
            {
                shown += candidate.Bytes;
            }
        }

        string line = item.Name;
        if (item.OtherVolume)
        {
            line += "  other volume";
        }
        else
        {
            line += "  " + EntryText.FormatSize(item.Bytes);
            if (shown > 0)
            {
                line += $"  {item.Bytes * 100 / shown}%";
            }
        }

        if (_measured is { Skipped: > 0 } measured)
        {
            line += measured.Skipped == 1 ? "  · 1 skipped" : $"  · {measured.Skipped} skipped";
        }

        if (_scan is not null)
        {
            string progress = _scanFiles > 0 ? $"Measuring… {_scanFiles}" : "Measuring…";
            line = progress + "  ·  " + line;
        }
        else if (_scanNote is not null)
        {
            line = _scanNote + "  ·  " + line;
        }

        return line + "   Esc close";
    }

    private bool GoUp()
    {
        if (DirectoryNavigation.Up(_directory) is { } move)
        {
            OpenDirectory(move.Directory, move.SelectName);
        }

        return true;
    }

    private bool OpenCurrent(bool openFile)
    {
        if (Current() is not { } item || item.Group || item.OtherVolume)
        {
            return true;
        }

        if (item.Directory)
        {
            OpenDirectory(Path.Combine(_directory, item.Name), null);
            return true;
        }

        if (openFile)
        {
            _error = _opener.Open(Path.Combine(_directory, item.Name));
            SetNeedsDraw();
        }

        return true;
    }

    private bool MoveCursor(int dx, int dy)
    {
        int width = Math.Max(0, Viewport.Width);
        int height = MapHeight(Math.Max(0, Viewport.Height));
        if (!TryLayout(width, height, out List<MapItem> items, out IReadOnlyList<MapBlock> blocks))
        {
            return true;
        }

        int next = Neighbor(blocks, IndexOf(items), dx, dy);
        if (next >= 0)
        {
            Remember(items[next]);
            SetNeedsDraw();
        }

        return true;
    }

    private bool MoveEnd(bool first)
    {
        int width = Math.Max(0, Viewport.Width);
        int height = MapHeight(Math.Max(0, Viewport.Height));
        if (!TryLayout(width, height, out List<MapItem> items, out _))
        {
            return true;
        }

        Remember(first ? items[0] : items[^1]);
        SetNeedsDraw();
        return true;
    }

    private MapItem? Current()
    {
        int width = Math.Max(0, Viewport.Width);
        int height = MapHeight(Math.Max(0, Viewport.Height));
        if (!TryLayout(width, height, out List<MapItem> items, out _))
        {
            return null;
        }

        return items[IndexOf(items)];
    }

    private bool TryLayout(int width, int height, out List<MapItem> items, out IReadOnlyList<MapBlock> blocks)
    {
        items = BuildItems(Math.Max(1, width * height));
        if (items.Count == 0 || width <= 0 || height <= 0)
        {
            blocks = [];
            return false;
        }

        var weights = new long[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            weights[i] = items[i].Weight;
        }

        blocks = TreeMapLayout.Arrange(width, height, weights);
        return blocks.Count == items.Count;
    }

    private List<MapItem> BuildItems(int cells)
    {
        if (_measured is not { } measured || !string.Equals(_measuredDirectory, _directory, StringComparison.Ordinal))
        {
            return [];
        }

        var visible = new List<FolderChild>();
        foreach (FolderChild child in measured.Children)
        {
            if (!_showHidden && child.Name.StartsWith('.'))
            {
                continue;
            }

            visible.Add(child);
        }

        if (visible.Count == 0)
        {
            return [];
        }

        long total = 0;
        foreach (FolderChild child in visible)
        {
            if (!child.OtherVolume)
            {
                total += Math.Max(0, child.Bytes);
            }
        }

        int area = Math.Max(1, cells);
        if (total <= 0)
        {
            var equal = new List<MapItem>(visible.Count);
            foreach (FolderChild child in visible)
            {
                equal.Add(new MapItem(child.Name, child.Bytes, 1, child.Directory, child.OtherVolume, false));
            }

            return equal;
        }

        long oneCell = Math.Max(1, total / area);
        int namedCap = visible.Count > area ? Math.Max(1, area - 1) : area;
        var items = new List<MapItem>();
        int named = 0;
        long otherBytes = 0;
        int otherCount = 0;
        foreach (FolderChild child in visible)
        {
            if (child.OtherVolume)
            {
                items.Add(new MapItem(child.Name, 0, oneCell, true, true, false));
                continue;
            }

            if (child.Bytes >= oneCell && named < namedCap)
            {
                items.Add(new MapItem(child.Name, child.Bytes, child.Bytes, child.Directory, false, false));
                named++;
                continue;
            }

            otherBytes += Math.Max(0, child.Bytes);
            otherCount++;
        }

        if (named == 0)
        {
            FolderChild largest = visible[0];
            if (!largest.OtherVolume && largest.Bytes > 0)
            {
                items.Add(new MapItem(largest.Name, largest.Bytes, Math.Max(largest.Bytes, 1), largest.Directory, false, false));
                otherBytes -= largest.Bytes;
                otherCount--;
            }
        }

        if (otherCount > 0 && otherBytes > 0)
        {
            items.Add(new MapItem("other", otherBytes, otherBytes, false, false, true));
        }

        return items;
    }

    private int IndexOf(IReadOnlyList<MapItem> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Group)
            {
                if (_cursorName == OtherCursor)
                {
                    return i;
                }

                continue;
            }

            if (_cursorName is not null && EntryNames.Same(items[i].Name, _cursorName))
            {
                return i;
            }
        }

        return 0;
    }

    private void Remember(MapItem item) => _cursorName = item.Group ? OtherCursor : item.Name;

    private static int Neighbor(IReadOnlyList<MapBlock> blocks, int index, int dx, int dy)
    {
        if ((uint)index >= (uint)blocks.Count)
        {
            return -1;
        }

        MapBlock from = blocks[index];
        double cx = from.X + (from.Width / 2.0);
        double cy = from.Y + (from.Height / 2.0);
        int best = -1;
        double bestScore = double.MaxValue;
        for (int i = 0; i < blocks.Count; i++)
        {
            MapBlock block = blocks[i];
            if (i == index || block.Width <= 0 || block.Height <= 0)
            {
                continue;
            }

            double x = block.X + (block.Width / 2.0);
            double y = block.Y + (block.Height / 2.0);
            double vx = x - cx;
            double vy = y - cy;
            if (dx != 0 && Math.Sign(vx) != dx)
            {
                continue;
            }

            if (dy != 0 && Math.Sign(vy) != dy)
            {
                continue;
            }

            double score = (vx * vx) + (vy * vy);
            if (score < bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    private void PaintBlock(MapItem item, MapBlock block, bool cursor, int index)
    {
        SetAttribute(cursor ? (HasFocus ? Graphite.Cursor : Graphite.InactiveCursor) : item.Directory ? Graphite.Directory : Graphite.Text);
        char fill = cursor ? ' ' : index % 2 == 0 ? '░' : '▒';
        string row = new string(fill, block.Width);
        for (int y = 0; y < block.Height; y++)
        {
            AddStr(block.X, block.Y + y, row);
        }

        if (block.Width < 2 || block.Height < 1)
        {
            return;
        }

        string name = EntryText.EllipsizeMiddle(item.Name, block.Width, text => text.GetColumns());
        AddStr(block.X, block.Y, name);
        if (block.Height < 2 || block.Width < 3)
        {
            return;
        }

        string detail = item.OtherVolume ? "volume" : EntryText.FormatSize(item.Bytes);
        detail = EntryText.EllipsizeMiddle(detail, block.Width, text => text.GetColumns());
        AddStr(block.X, block.Y + 1, detail);
    }

    private static int MapHeight(int height) => Math.Max(0, height - 2);

    private static string Fit(string text, int width)
    {
        string clipped = EntryText.EllipsizeMiddle(text, width, value => value.GetColumns());
        int gap = width - clipped.GetColumns();
        return gap > 0 ? clipped + new string(' ', gap) : clipped;
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

    private readonly record struct MapItem(string Name, long Bytes, long Weight, bool Directory, bool OtherVolume, bool Group);

    private sealed record ScanDelivery(int Generation, string Directory, FolderMeasureResult? Result, bool Cancelled);
}
