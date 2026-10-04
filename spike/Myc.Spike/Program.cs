using System.Diagnostics;
using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.Spike;

/// <summary>
/// Throwaway probe for the questions in plan/01-framework.md.
/// Run it, then throw the project away once the decision is recorded.
/// </summary>
public static class Program
{
    private const int EntryCount = SpikeCatalog.Count;

    public static int Main(string[] args)
    {
        if (args.Contains("--self-check"))
        {
            return SelfCheck();
        }

        string? themeError = LoadThemes();
        Application.SetDefaultKeyBinding(Command.Quit, Bind.All(Key.F10, Key.Q.WithCtrl));

        using IApplication app = Application.Create();
        app.Init();

        IReadOnlyList<SpikeEntry> entries = SpikeCatalog.Create();
        var panel = new FilePanelView(entries);
        TableView table = CreateTable(entries);
        var log = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = 2 };
        var hint = new Label
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
            Text = "Space mark+down · t theme · F5 copy · F8 delete · Esc then digit = F-key · F10 quit",
        };

        panel.X = 0;
        panel.Y = 2;
        panel.Width = Dim.Percent(50);
        panel.Height = Dim.Fill(1);

        table.X = Pos.Right(panel);
        table.Y = 2;
        table.Width = Dim.Fill();
        table.Height = Dim.Fill(1);
        table.CanFocus = true;
        table.BorderStyle = LineStyle.Rounded;

        var window = new Window { Title = $"myc spike  Terminal.Gui {typeof(View).Assembly.GetName().Version}" };
        window.Add(log, panel, table, hint);

        var probe = new KeyProbe(app, window, panel, table, log, themeError);
        panel.Changed += probe.Refresh;
        table.ValueChanged += (_, _) => probe.Refresh();
        app.Keyboard.KeyDown += probe.OnKeyDown;
        probe.Refresh();

        panel.SetFocus();
        app.Run(window);
        app.Dispose();
        return 0;
    }

    private static string? LoadThemes()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "themes", "paper.json");
        if (!File.Exists(path))
        {
            return $"theme JSON missing: {path}";
        }

        var builder = new TuiConfigurationBuilder("myc-spike")
        {
            RuntimeConfig = File.ReadAllText(path),
        };
        builder.ApplyToStaticFacades();

        string errors = string.Join(" | ", TuiJsonErrors.GetErrors());
        if (errors.Length > 0)
        {
            return errors;
        }

        return builder.ThemeManager.CurrentThemeName == "Paper"
            ? null
            : $"expected current theme Paper, got {builder.ThemeManager.CurrentThemeName}";
    }

    /// <summary>
    /// Terminal.Gui 2.5's <c>SwitchTheme</c> only accepts built-in theme names. A theme defined in
    /// runtime JSON is applied by pointing <c>RuntimeConfig</c> at it and publishing the facades again.
    /// </summary>
    internal static void ActivateJsonTheme(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "themes", "paper.json");
        string json = File.ReadAllText(path);
        TuiConfigurationBuilder.Shared.RuntimeConfig = json.Replace(
            "\"Theme\": \"Paper\"",
            $"\"Theme\": \"{name}\"",
            StringComparison.Ordinal);
        TuiConfigurationBuilder.Shared.ApplyToStaticFacades();
    }

    private static TableView CreateTable(IReadOnlyList<SpikeEntry> entries)
    {
        var source = new SpikeTableSource(entries);
        var table = new TableView
        {
            Table = source,
            FullRowSelect = true,
            MultiSelect = true,
            MaxCellWidth = 36,
        };
        table.Table = new CheckBoxTableSourceWrapperByIndex(table, source);
        table.Style.ShowHorizontalHeaderOverline = false;
        table.Style.ShowVerticalCellLines = false;
        return table;
    }

    /// <summary>
    /// Answers the parts of the spike that do not need a terminal: Unicode column
    /// widths, and how long it takes to format one screen of a 50k list.
    /// </summary>
    private static int SelfCheck()
    {
        IReadOnlyList<SpikeEntry> entries = SpikeCatalog.Create();
        var report = new StringBuilder();
        report.AppendLine($"entries={entries.Count}");
        report.AppendLine($"keyIsValueType={typeof(Key).IsValueType}");
        string? themeError = LoadThemes();
        string themes = string.Join(", ", TuiConfigurationBuilder.Shared.ThemeManager.ThemeNames);
        string paper = SchemeManager.GetScheme("Base")?.ToString() ?? "";
        bool switchApi = TuiConfigurationBuilder.Shared.ThemeManager.SwitchTheme("Graphite");
        ActivateJsonTheme("Graphite");
        string graphite = SchemeManager.GetScheme("Base")?.ToString() ?? "";
        bool paperOk = paper.Contains("#1F2328", StringComparison.Ordinal) && paper.Contains("#F6F8FA", StringComparison.Ordinal);
        bool graphiteOk = graphite.Contains("#C9CCD1", StringComparison.Ordinal) && graphite.Contains("#4FB8A8", StringComparison.Ordinal);
        report.AppendLine($"themes={themes}");
        report.AppendLine($"themeLoad={(themeError ?? "ok")} paperApplied={paperOk} graphiteApplied={graphiteOk} switchThemeSeesCustom={switchApi}");
        if (!paperOk || !graphiteOk)
        {
            Console.WriteLine(report.ToString());
            return 1;
        }

        foreach (SpikeEntry entry in entries.Take(8))
        {
            string nfc = entry.Name.Normalize(NormalizationForm.FormC);
            string nfd = entry.Name.Normalize(NormalizationForm.FormD);
            report.AppendLine(
                $"name={Escape(entry.Name)} note={entry.Note} cols={entry.Name.GetColumns()} " +
                $"nfc==nfd={nfc == nfd} nfcCols={nfc.GetColumns()} nfdCols={nfd.GetColumns()}");
        }

        const int rows = 40;
        const int columns = 60;
        const int frames = 2_000;
        long start = Stopwatch.GetTimestamp();
        int checksum = 0;
        for (int frame = 0; frame < frames; frame++)
        {
            int first = frame % (entries.Count - rows);
            for (int row = 0; row < rows; row++)
            {
                checksum += FilePanelView.FormatRow(entries[first + row], row % 3 == 0, columns).Length;
            }
        }

        double microseconds = (Stopwatch.GetTimestamp() - start) * 1_000_000.0 / Stopwatch.Frequency / frames;
        report.AppendLine($"format {rows}x{columns} avg={microseconds:0.0}µs over {frames} frames checksum={checksum}");
        Console.WriteLine(report.ToString());
        return entries.Count == EntryCount && checksum > 0 ? 0 : 1;
    }

    private static string Escape(string value) => value.Replace("\u0301", "\\u0301").Replace("\u0308", "\\u0308");
}

/// <summary>Records every key the driver delivers, including F-keys and the Esc+digit fallback.</summary>
public sealed class KeyProbe
{
    private readonly IApplication _app;
    private readonly Window _window;
    private readonly FilePanelView _panel;
    private readonly TableView _table;
    private readonly Label _log;
    private readonly string? _themeError;
    private readonly Scheme _graphite = new()
    {
        Normal = new Terminal.Gui.Drawing.Attribute("#C9CCD1", "#1E2127"),
        Focus = new Terminal.Gui.Drawing.Attribute("#E8EAED", "#2E3440", TextStyle.Bold),
        Highlight = new Terminal.Gui.Drawing.Attribute("#4FB8A8", "#1E2127", TextStyle.Bold),
    };

    private readonly List<string> _recent = [];
    private readonly SortedSet<string> _functionKeys = [];
    private bool _escArmed;
    private int _theme;
    private string _fallback = "";

    public KeyProbe(
        IApplication app,
        Window window,
        FilePanelView panel,
        TableView table,
        Label log,
        string? themeError)
    {
        _app = app;
        _window = window;
        _panel = panel;
        _table = table;
        _log = log;
        _themeError = themeError;
        ApplyTheme();
    }

    public void OnKeyDown(object? sender, Key key)
    {
        Remember(key.ToString());
        if (key == Key.F1 || key == Key.F2 || key == Key.F3 || key == Key.F4 || key == Key.F5
            || key == Key.F6 || key == Key.F7 || key == Key.F8 || key == Key.F9 || key == Key.F10
            || key == Key.F11 || key == Key.F12
            || key == Key.F1.WithShift || key == Key.F2.WithShift || key == Key.F5.WithShift
            || key == Key.F6.WithShift || key == Key.F8.WithShift || key == Key.F10.WithShift)
        {
            _functionKeys.Add(key.ToString() ?? "?");
        }

        if (_escArmed)
        {
            _escArmed = false;
            int? function = FunctionFromDigit(key);
            if (function is int number)
            {
                _fallback = $"Esc {number % 10} → F{number}";
                _functionKeys.Add(_fallback);
                key.Handled = true;
                Refresh();
                return;
            }
        }

        if (key == Key.Esc)
        {
            _escArmed = true;
            _fallback = "Esc armed";
        }
        else if (key == Key.T.WithShift || key == Key.T)
        {
            _theme = (_theme + 1) % 3;
            ApplyTheme();
            key.Handled = true;
        }
        else if (key == Key.F5)
        {
            key.Handled = true;
            int choice = MessageBox.Query(_app, "Copy", "Copy the marked items to the other panel? (spike: no files are touched)", "Copy", "Cancel") ?? -1;
            _fallback = choice == 0 ? "F5 copy confirmed" : "F5 cancelled";
        }
        else if (key == Key.F8)
        {
            key.Handled = true;
            int choice = MessageBox.Query(_app, "Delete", "Move the marked items to Trash? (spike: nothing is deleted)", "Cancel", "Move to Trash") ?? -1;
            _fallback = choice == 1 ? "F8 confirmed" : "F8 cancelled";
        }

        Refresh();
    }

    public void Refresh()
    {
        _panel.RefreshTitle();
        int checks = _table.Table is CheckBoxTableSourceWrapperByIndex boxes ? boxes.CheckedRows.Count : 0;
        _table.Title = $"table  {checks} checked  {SpikeCatalog.Count} rows";

        string themes = string.Join(", ", TuiConfigurationBuilder.Shared.ThemeManager.ThemeNames);
        string theme = _theme switch
        {
            0 => "code Graphite (transparent background)",
            1 => "JSON Graphite",
            _ => "JSON Paper",
        };
        string error = _themeError is null ? "" : $"  JSON: {_themeError}";
        _log.Text =
            $"last: {string.Join("  ", _recent)}   {_fallback}{error}\n" +
            $"theme: {theme}   available: {themes}   F-keys seen: {string.Join(" ", _functionKeys)}";
    }

    private void ApplyTheme()
    {
        switch (_theme)
        {
            case 0:
                _window.SetScheme(_graphite);
                break;
            case 1:
                Program.ActivateJsonTheme("Graphite");
                _window.SetScheme(SchemeManager.GetScheme("Base"));
                break;
            default:
                Program.ActivateJsonTheme("Paper");
                _window.SetScheme(SchemeManager.GetScheme("Base"));
                break;
        }

        _window.SetNeedsDraw();
    }

    private void Remember(string? text)
    {
        _recent.Add(text ?? "?");
        if (_recent.Count > 6)
        {
            _recent.RemoveAt(0);
        }
    }

    private static int? FunctionFromDigit(Key key)
    {
        Key plain = key.NoShift.NoCtrl.NoAlt;
        if (plain == Key.D0) return 10;
        if (plain == Key.D1) return 1;
        if (plain == Key.D2) return 2;
        if (plain == Key.D3) return 3;
        if (plain == Key.D4) return 4;
        if (plain == Key.D5) return 5;
        if (plain == Key.D6) return 6;
        if (plain == Key.D7) return 7;
        if (plain == Key.D8) return 8;
        if (plain == Key.D9) return 9;
        return null;
    }
}

public sealed class SpikeTableSource(IReadOnlyList<SpikeEntry> entries) : ITableSource
{
    public int Rows => entries.Count;

    public int Columns => 2;

    public string[] ColumnNames { get; } = ["Name", "Note"];

    public object this[int row, int col] => col == 0 ? entries[row].Name : entries[row].Note;
}
