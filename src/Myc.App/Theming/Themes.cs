using Myc.Core.Configuration;
using Terminal.Gui.Configuration;

namespace Myc.App.Theming;

/// <summary>
/// Applies a theme name to the panel roles. Call it after <c>Application.Init</c>.
/// Graphite and Paper are myc palettes. Anything else is a Terminal.Gui built-in theme.
/// </summary>
internal static class Themes
{
    private static readonly bool Plain = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

    public static string Current { get; private set; } = ThemeNames.Graphite;

    /// <summary>Raised after a theme replaces the roles, so the open window can redraw.</summary>
    public static event Action? Changed;

    /// <summary>Sets the palette before the window exists. Nothing is redrawn.</summary>
    public static void Use(string name) => Apply(name, notify: false);

    /// <summary>Sets the palette and tells the window to redraw. Enter on the theme dialog does this.</summary>
    public static void Apply(string name) => Apply(name, notify: true);

    private static void Apply(string name, bool notify)
    {
        string canonical = ThemeNames.Canonical(name);
        Current = canonical;
        if (canonical == ThemeNames.Paper)
        {
            Graphite.LoadPaper();
        }
        else if (canonical == ThemeNames.Graphite || Plain)
        {
            Graphite.LoadGraphite();
        }
        else if (TuiConfigurationBuilder.Shared.ThemeManager.SwitchTheme(canonical))
        {
            Graphite.LoadLibrary();
        }
        else
        {
            Current = ThemeNames.Graphite;
            Graphite.LoadGraphite();
        }

        if (notify)
        {
            Changed?.Invoke();
        }
    }
}
