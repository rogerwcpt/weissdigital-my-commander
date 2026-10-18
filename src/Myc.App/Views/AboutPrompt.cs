using System.Reflection;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Myc.App.Theming;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Views;

/// <summary>
/// Options → About. The mark is the two panels, the version is the one this build was
/// published with, and the link opens the repository.
/// </summary>
internal static class AboutPrompt
{
    public const string RepositoryUrl = "https://github.com/rogerwcpt/weissdigital-my-commander";

    private const string RepositoryLabel = "github.com/rogerwcpt/weissdigital-my-commander";

    public static void Show(IApplication app)
    {
        var dialog = new Dialog
        {
            Title = "About",
            Width = 54,
            Height = 14,
        };
        dialog.SetScheme(Graphite.Bar);
        dialog.Add(new CommanderMark { X = 2, Y = 0 });
        dialog.Add(Caption("myc", 1, Graphite.Directory));
        dialog.Add(Caption(VersionLabel(), 2, Graphite.Header));
        dialog.Add(Caption("A dual-panel file manager.", 3, Graphite.Text));
        dialog.Add(Caption("Weiss Digital", 4, Graphite.Text));
        var link = new Link
        {
            Text = RepositoryLabel,
            Url = RepositoryUrl,
            X = 2,
            Y = 7,
            CanFocus = true,
        };
        link.SetScheme(LinkScheme());
        dialog.Add(link);
        dialog.AddButton(new Button { Text = "Close", IsDefault = true, HotKey = Key.Empty });
        app.Run(dialog);
    }

    /// <summary>What the About box prints. A local build is <c>dev</c>; a release is <c>v0.1.2</c>.</summary>
    internal static string VersionLabel() => Format(Informational());

    internal static string Format(string? informational)
    {
        string version = (informational ?? "").Split('+', 2)[0].Trim();
        if (version.Length == 0 || version is "0.0.0" or "0.0.0-dev")
        {
            return "dev";
        }

        return version[0] is 'v' or 'V' ? version : "v" + version;
    }

    private static string Informational() =>
        typeof(AboutPrompt).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "";

    private static Label Caption(string text, int y, Attribute color)
    {
        var label = new Label
        {
            Text = text,
            X = 15,
            Y = y,
            Width = text.Length,
            Height = 1,
            CanFocus = false,
        };
        label.SetScheme(Flat(color));
        return label;
    }

    private static Scheme Flat(Attribute color) => new()
    {
        Normal = color,
        Focus = color,
        Highlight = color,
        Disabled = color,
        HotNormal = color,
        HotFocus = color,
        Active = color,
    };

    private static Scheme LinkScheme()
    {
        Attribute linked = Graphite.Marked with { Style = Graphite.Marked.Style | TextStyle.Underline };
        Attribute focus = Graphite.Bar.Focus with { Style = Graphite.Bar.Focus.Style | TextStyle.Underline };
        return new Scheme
        {
            Normal = linked,
            Focus = focus,
            Highlight = linked,
            Disabled = Graphite.Header,
            HotNormal = linked,
            HotFocus = focus,
            Active = focus,
        };
    }

    /// <summary>
    /// The product mark: the panel that has the keyboard, and the other one beside it.
    /// </summary>
    private sealed class CommanderMark : View
    {
        public CommanderMark()
        {
            Width = 11;
            Height = 6;
            CanFocus = false;
            SetScheme(Graphite.Bar);
        }

        protected override bool OnDrawingContent(DrawContext? context)
        {
            Color background = Graphite.Bar.Normal.Background;
            DrawBox(0, WithBackground(Graphite.Scheme.Normal, background), cursor: true);
            DrawBox(6, WithBackground(Graphite.Inactive.Normal, background), cursor: false);
            return true;
        }

        private void DrawBox(int x, Attribute border, bool cursor)
        {
            SetAttribute(border);
            AddStr(x, 0, "┌───┐");
            AddStr(x, 1, "│   │");
            AddStr(x, 2, "│   │");
            AddStr(x, 3, "│   │");
            AddStr(x, 4, "│   │");
            AddStr(x, 5, "└───┘");
            if (!cursor)
            {
                return;
            }

            // The same cursor the live panel draws, so the left box reads as the one with the keyboard.
            SetAttribute(Graphite.Cursor);
            AddStr(x + 1, 2, " ");
        }

        private static Attribute WithBackground(Attribute source, Color background) =>
            new(source.Foreground, background, source.Style);
    }
}
