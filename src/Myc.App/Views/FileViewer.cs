using Attribute = Terminal.Gui.Drawing.Attribute;
using Myc.App.Theming;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Myc.App.Views;

/// <summary>
/// Full-screen file viewer. F3 and Esc close it. W toggles wrap on the source.
/// M switches between source and rendered Markdown.
/// </summary>
internal sealed class FileViewer : Window
{
    private readonly string _text;
#pragma warning disable CS0618 // Read-only source view. The replacement editor is a separate package.
    private readonly TextView _source;
#pragma warning restore CS0618
    private readonly Label _status;
    private readonly bool _markdownAllowed;
    private Markdown? _markdown;
    private Scheme _body = null!;
    private bool _rendered;

    private FileViewer(string name, string text, bool rendered, bool markdown)
    {
        Title = name;
        _text = text;
        _rendered = rendered;
        _markdownAllowed = markdown;
#pragma warning disable CS0618
        _source = new TextView
#pragma warning restore CS0618
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ReadOnly = true,
            Multiline = true,
            WordWrap = true,
            Text = text,
        };
        _status = new Label
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
            CanFocus = false,
        };
        Add(_source, _status);
        Restyle();
        if (rendered)
        {
            ShowRendered();
        }

        UpdateStatus();
        Themes.Changed += Restyle;
        Initialized += (_, _) => FocusBody();
    }

    /// <param name="markdown">When false, M does nothing. Inspection pages leave it off.</param>
    public static void Show(IApplication app, string name, string text, bool rendered, bool markdown = true)
    {
        using var viewer = new FileViewer(name, text, rendered, markdown);
        app.Run(viewer);
    }

    public bool AllowsMarkdown => _markdownAllowed;

    public void ToggleWrap()
    {
        _source.WordWrap = !_source.WordWrap;
        UpdateStatus();
    }

    public void ToggleMarkdown()
    {
        _rendered = !_rendered;
        if (_rendered)
        {
            ShowRendered();
        }
        else
        {
            if (_markdown is not null)
            {
                _markdown.Visible = false;
            }

            _source.Visible = true;
            _source.SetFocus();
        }

        UpdateStatus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Themes.Changed -= Restyle;
        }

        base.Dispose(disposing);
    }

    private void ShowRendered()
    {
        _markdown ??= new Markdown
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            UseThemeBackground = false,
            ShowCopyButtons = false,
            EnableSixelImages = false,
            Text = _text,
        };
        _markdown.SetScheme(_body);
        if (_markdown.SuperView is null)
        {
            Add(_markdown);
        }

        _source.Visible = false;
        _markdown.Visible = true;
        _markdown.SetFocus();
    }

    private void FocusBody()
    {
        if (_rendered && _markdown is not null)
        {
            _markdown.SetFocus();
            return;
        }

        _source.SetFocus();
    }

    private void Restyle()
    {
        SetScheme(Graphite.Scheme);
        Attribute text = Graphite.Text;
        _body = new Scheme
        {
            Normal = text,
            Focus = text,
            Highlight = Graphite.Marked,
            Disabled = Graphite.Header,
            HotNormal = text,
            HotFocus = text,
            Active = text,
            Code = text with { Style = text.Style | TextStyle.Faint },
        };
        var hint = new Scheme
        {
            Normal = Graphite.Header,
            Focus = Graphite.Header,
            Highlight = Graphite.Header,
            Disabled = Graphite.Header,
        };
        _source.SetScheme(_body);
        _markdown?.SetScheme(_body);
        _status.SetScheme(hint);
        SetNeedsDraw();
    }

    private void UpdateStatus()
    {
        string wrap = _source.WordWrap ? "wrap on" : "wrap off";
        if (!_markdownAllowed)
        {
            _status.Text = $"F3 close   W {wrap}";
            return;
        }

        string mode = _rendered ? "rendered" : "source";
        _status.Text = $"F3 close   W {wrap}   M {mode}";
    }
}
