namespace Myc.Core.Configuration;

/// <summary>
/// Theme names myc knows how to store. Graphite and Paper are ours. The rest are
/// Terminal.Gui's built-in themes, spelled the way <c>SwitchTheme</c> expects.
/// </summary>
public static class ThemeNames
{
    public const string Graphite = AppSettings.DefaultTheme;
    public const string Paper = "Paper";

    public static readonly string[] All =
    [
        Graphite,
        Paper,
        "TurboPascal 5",
        "Anders",
        "Dark",
        "Light",
        "Green Phosphor",
        "Amber Phosphor",
        "8-Bit",
    ];

    /// <summary>The stored spelling, or Graphite when the file names something myc does not ship.</summary>
    public static string Canonical(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Graphite;
        }

        foreach (string known in All)
        {
            if (string.Equals(known, name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }

        return Graphite;
    }
}
