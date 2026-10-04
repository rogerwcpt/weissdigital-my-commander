namespace Myc.Core.Configuration;

/// <summary>Which questions myc asks before it changes the disk.</summary>
public sealed record ConfirmationSettings
{
    public bool Delete { get; init; } = true;

    public bool Overwrite { get; init; } = true;

    public bool Transfer { get; init; }

    public static ConfirmationSettings Default { get; } = new();
}

/// <summary>
/// Preferences in <c>~/.config/myc/settings.json</c>. <see cref="Startup"/> is <c>restore</c>
/// (reopen the last folders) or <c>default</c> (left is the working directory, right is home).
/// </summary>
public sealed record AppSettings
{
    public const string RestoreStartup = "restore";
    public const string DefaultStartup = "default";
    public const string DefaultTheme = "Graphite";

    public ConfirmationSettings Confirm { get; init; } = ConfirmationSettings.Default;

    public string Theme { get; init; } = DefaultTheme;

    public string Startup { get; init; } = RestoreStartup;

    public static AppSettings Default { get; } = new();

    public AppSettings Normalized()
    {
        ConfirmationSettings confirm = Confirm;
        if (ReferenceEquals(confirm, null))
        {
            confirm = ConfirmationSettings.Default;
        }

        return this with
        {
            Confirm = confirm,
            Theme = ThemeNames.Canonical(Theme),
            Startup = Startup == DefaultStartup ? DefaultStartup : RestoreStartup,
        };
    }
}
