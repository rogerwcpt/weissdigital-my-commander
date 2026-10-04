using Attribute = Terminal.Gui.Drawing.Attribute;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;

namespace Myc.App.Theming;

/// <summary>
/// The colours the panels, dialogs, and function bar actually paint with. Graphite is the
/// starting palette. <see cref="Themes"/> replaces every role when the user picks another one.
/// When <c>NO_COLOR</c> is set, every role keeps the terminal's own colours and uses bold,
/// faint, or inverse instead.
/// </summary>
internal static class Graphite
{
    private static readonly bool Plain = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

    public static Scheme Scheme { get; private set; } = null!;

    /// <summary>Dim border and title for the panel that does not have the keyboard.</summary>
    public static Scheme Inactive { get; private set; } = null!;

    /// <summary>Delete confirmation. Muted red so the dialog reads as destructive.</summary>
    public static Scheme Danger { get; private set; } = null!;

    /// <summary>Function-key bar and ordinary dialogs. Disabled entries are the keys that are not implemented yet.</summary>
    public static Scheme Bar { get; private set; } = null!;

    public static Attribute Text { get; private set; }

    public static Attribute Directory { get; private set; }

    public static Attribute Hidden { get; private set; }

    public static Attribute HiddenDirectory { get; private set; }

    public static Attribute Header { get; private set; }

    public static Attribute Marked { get; private set; }

    public static Attribute InactiveCursor { get; private set; }

    public static Attribute Cursor { get; private set; }

    public static Attribute CursorMarked { get; private set; }

    public static Attribute Rule { get; private set; }

    static Graphite() => LoadGraphite();

    /// <summary>Dark neutral palette. Teal is the accent; row text does not inherit it.</summary>
    public static void LoadGraphite()
    {
        const string background = "#1E2127";
        const string accent = "#4FB8A8";
        Attribute border = Paint(accent, background, TextStyle.Bold, TextStyle.Bold);
        Attribute text = Paint("#C9CCD1", background);
        Attribute cursor = Paint("#E8EAED", "#2E3440", TextStyle.Bold, TextStyle.Reverse);
        Attribute marked = Paint(accent, background, TextStyle.Bold, TextStyle.Bold);

        Text = text;
        Directory = Paint("#E8EAED", background, TextStyle.Bold, TextStyle.Bold);
        Hidden = Paint("#6B7280", background, plain: TextStyle.Faint);
        HiddenDirectory = Paint("#8B93A0", background, TextStyle.Bold, TextStyle.Faint);
        Header = Paint("#8B93A0", background, plain: TextStyle.Faint);
        Marked = marked;
        InactiveCursor = Paint("#C9CCD1", "#2A2E36", plain: TextStyle.Bold);
        Cursor = cursor;
        CursorMarked = Paint(accent, "#2E3440", TextStyle.Bold, TextStyle.Bold | TextStyle.Reverse);
        Rule = Paint("#5C6370", background);

        Scheme = Role(border, cursor, marked);
        Inactive = Role(Paint("#5C6370", background), Paint("#C9CCD1", "#2A2E36"), marked);
        Attribute danger = Paint("#E06C75", background, plain: TextStyle.Bold);
        Danger = Role(danger, Paint(background, "#E06C75", TextStyle.Bold, TextStyle.Reverse), danger);
        Bar = Role(text, Paint(background, accent, TextStyle.Bold, TextStyle.Reverse), text, Paint("#5C6370", background, plain: TextStyle.Faint));
    }

    /// <summary>Light palette from the spike. Teal accent on a paper background.</summary>
    public static void LoadPaper()
    {
        const string background = "#F6F8FA";
        const string ink = "#1F2328";
        const string accent = "#0F6E6A";
        const string sheet = "#FFFFFF";
        Attribute text = Paint(ink, background);
        Attribute cursor = Paint(ink, "#D0E2FF", TextStyle.Bold, TextStyle.Reverse);
        Attribute marked = Paint(accent, background, TextStyle.Bold, TextStyle.Bold);
        Attribute quiet = Paint("#6E7781", background, plain: TextStyle.Faint);

        Text = text;
        Directory = Paint(ink, background, TextStyle.Bold, TextStyle.Bold);
        Hidden = quiet;
        HiddenDirectory = Paint("#57606A", background, TextStyle.Bold, TextStyle.Faint);
        Header = quiet;
        Marked = marked;
        InactiveCursor = Paint(ink, "#EAEFF3", plain: TextStyle.Bold);
        Cursor = cursor;
        CursorMarked = Paint(accent, "#D0E2FF", TextStyle.Bold, TextStyle.Bold | TextStyle.Reverse);
        Rule = Paint("#D0D7DE", background);

        Scheme = Role(Paint(accent, background, TextStyle.Bold, TextStyle.Bold), cursor, marked);
        Inactive = Role(Paint("#8C959F", background), InactiveCursor, marked);
        Attribute danger = Paint("#CF222E", sheet, plain: TextStyle.Bold);
        Danger = Role(danger, Paint(sheet, "#CF222E", TextStyle.Bold, TextStyle.Reverse), danger);
        Bar = Role(Paint(ink, sheet), Paint(sheet, accent, TextStyle.Bold, TextStyle.Reverse), Paint(ink, sheet), Paint("#8C959F", sheet, plain: TextStyle.Faint));
    }

    /// <summary>Panel roles taken from the library theme that was just activated.</summary>
    public static void LoadLibrary()
    {
        Scheme basis = SchemeManager.GetScheme(Schemes.Base);
        Scheme dialog = SchemeManager.GetScheme(Schemes.Dialog);
        Attribute normal = basis.Normal;
        Attribute focus = basis.Focus;
        Attribute highlight = basis.Highlight;
        Attribute disabled = basis.Disabled;
        Color background = normal.Background;
        Color foreground = normal.Foreground;
        Color accent = highlight.Foreground;
        Color danger = background.IsDarkColor() ? new Color("#E06C75") : new Color("#A40E26");

        Text = new Attribute(foreground, background);
        Directory = new Attribute(foreground, background, TextStyle.Bold);
        Hidden = new Attribute(disabled.Foreground, background, TextStyle.Faint);
        HiddenDirectory = new Attribute(disabled.Foreground, background, TextStyle.Bold | TextStyle.Faint);
        Header = Hidden;
        Marked = new Attribute(accent, background, TextStyle.Bold);
        InactiveCursor = new Attribute(disabled.Foreground, focus.Background, TextStyle.Bold);
        Cursor = focus;
        CursorMarked = new Attribute(accent, focus.Background, TextStyle.Bold | TextStyle.Reverse);
        Rule = new Attribute(disabled.Foreground, background);

        Scheme = Role(new Attribute(accent, background, TextStyle.Bold), focus, Marked);
        Inactive = Role(new Attribute(disabled.Foreground, background), InactiveCursor, Marked);
        Attribute dangerNormal = new Attribute(danger, background, TextStyle.Bold);
        Danger = Role(dangerNormal, new Attribute(background, danger, TextStyle.Bold | TextStyle.Reverse), dangerNormal);
        Bar = Role(dialog.Normal, dialog.Focus, dialog.Normal, disabled);
    }

    private static Scheme Role(Attribute normal, Attribute focus, Attribute highlight, Attribute? disabled = null) => new()
    {
        Normal = normal,
        Focus = focus,
        Highlight = highlight,
        Disabled = disabled ?? normal,
    };

    private static Attribute Paint(
        string foreground,
        string background,
        TextStyle colored = TextStyle.None,
        TextStyle plain = TextStyle.None) =>
        Plain
            ? new Attribute(Color.None, Color.None, plain)
            : new Attribute(foreground, background, colored);
}
