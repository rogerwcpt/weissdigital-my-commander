using Attribute = Terminal.Gui.Drawing.Attribute;
using Terminal.Gui.Drawing;

namespace Myc.App.Theming;

/// <summary>
/// Dark neutral theme. The scheme's normal colour is the teal border; row colours are
/// applied explicitly so text does not inherit that accent. When <c>NO_COLOR</c> is set,
/// every role keeps the terminal's own colours and uses bold or inverse instead.
/// </summary>
internal static class Graphite
{
    private static readonly bool Plain = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

    public static Scheme Scheme { get; } = new()
    {
        Normal = Paint("#4FB8A8", "#1E2127", plain: TextStyle.Bold),
        Focus = Paint("#E8EAED", "#2E3440", TextStyle.Bold, TextStyle.Reverse),
        Highlight = Paint("#4FB8A8", "#1E2127", TextStyle.Bold, TextStyle.Bold),
    };

    /// <summary>Dim border and title for the panel that does not have the keyboard.</summary>
    public static Scheme Inactive { get; } = new()
    {
        Normal = Paint("#5C6370", "#1E2127"),
        Focus = Paint("#C9CCD1", "#2A2E36"),
        Highlight = Paint("#4FB8A8", "#1E2127", TextStyle.Bold, TextStyle.Bold),
    };

    /// <summary>Delete confirmation. Muted red so the dialog reads as destructive.</summary>
    public static Scheme Danger { get; } = new()
    {
        Normal = Paint("#E06C75", "#1E2127", plain: TextStyle.Bold),
        Focus = Paint("#1E2127", "#E06C75", TextStyle.Bold, TextStyle.Reverse),
        Highlight = Paint("#E06C75", "#1E2127", TextStyle.Bold, TextStyle.Bold),
    };

    /// <summary>Function-key bar. Disabled entries are the keys that are not implemented yet.</summary>
    public static Scheme Bar { get; } = new()
    {
        Normal = Paint("#C9CCD1", "#1E2127"),
        Disabled = Paint("#5C6370", "#1E2127", plain: TextStyle.Faint),
        Focus = Paint("#1E2127", "#4FB8A8", TextStyle.Bold, TextStyle.Reverse),
    };

    public static Attribute Text { get; } = Paint("#C9CCD1", "#1E2127");

    public static Attribute Directory { get; } = Paint("#E8EAED", "#1E2127", TextStyle.Bold, TextStyle.Bold);

    public static Attribute Hidden { get; } = Paint("#6B7280", "#1E2127", plain: TextStyle.Faint);

    public static Attribute HiddenDirectory { get; } = Paint("#8B93A0", "#1E2127", TextStyle.Bold, TextStyle.Faint);

    public static Attribute Header { get; } = Paint("#8B93A0", "#1E2127", plain: TextStyle.Faint);

    public static Attribute Marked { get; } = Paint("#4FB8A8", "#1E2127", TextStyle.Bold, TextStyle.Bold);

    public static Attribute InactiveCursor { get; } = Paint("#C9CCD1", "#2A2E36", plain: TextStyle.Bold);

    public static Attribute Cursor { get; } = Paint("#E8EAED", "#2E3440", TextStyle.Bold, TextStyle.Reverse);

    public static Attribute CursorMarked { get; } = Paint("#4FB8A8", "#2E3440", TextStyle.Bold, TextStyle.Bold | TextStyle.Reverse);

    public static Attribute Rule { get; } = Paint("#5C6370", "#1E2127");

    private static Attribute Paint(
        string foreground,
        string background,
        TextStyle colored = TextStyle.None,
        TextStyle plain = TextStyle.None) =>
        Plain
            ? new Attribute(Color.None, Color.None, plain)
            : new Attribute(foreground, background, colored);
}
