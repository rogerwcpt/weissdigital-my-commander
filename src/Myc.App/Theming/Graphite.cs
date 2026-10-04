using Attribute = Terminal.Gui.Drawing.Attribute;
using Terminal.Gui.Drawing;

namespace Myc.App.Theming;

/// <summary>
/// Dark neutral theme. The scheme's normal colour is the teal border; row colours are
/// applied explicitly so text does not inherit that accent.
/// </summary>
internal static class Graphite
{
    public static Scheme Scheme { get; } = new()
    {
        Normal = new Attribute("#4FB8A8", "#1E2127"),
        Focus = new Attribute("#E8EAED", "#2E3440", TextStyle.Bold),
        Highlight = new Attribute("#4FB8A8", "#1E2127", TextStyle.Bold),
    };

    /// <summary>Dim border and title for the panel that does not have the keyboard.</summary>
    public static Scheme Inactive { get; } = new()
    {
        Normal = new Attribute("#5C6370", "#1E2127"),
        Focus = new Attribute("#C9CCD1", "#2A2E36"),
        Highlight = new Attribute("#4FB8A8", "#1E2127", TextStyle.Bold),
    };

    /// <summary>Function-key bar. Disabled entries are the keys that are not implemented yet.</summary>
    public static Scheme Bar { get; } = new()
    {
        Normal = new Attribute("#C9CCD1", "#1E2127"),
        Disabled = new Attribute("#5C6370", "#1E2127"),
        Focus = new Attribute("#1E2127", "#4FB8A8", TextStyle.Bold),
    };

    public static Attribute Text { get; } = new("#C9CCD1", "#1E2127");

    public static Attribute Directory { get; } = new("#E8EAED", "#1E2127", TextStyle.Bold);

    public static Attribute Header { get; } = new("#8B93A0", "#1E2127");

    public static Attribute Marked { get; } = new("#4FB8A8", "#1E2127", TextStyle.Bold);

    public static Attribute InactiveCursor { get; } = new("#C9CCD1", "#2A2E36");

    public static Attribute Cursor { get; } = new("#E8EAED", "#2E3440", TextStyle.Bold);

    public static Attribute CursorMarked { get; } = new("#4FB8A8", "#2E3440", TextStyle.Bold);

    public static Attribute Rule { get; } = new("#5C6370", "#1E2127");
}
