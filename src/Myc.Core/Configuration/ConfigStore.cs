using System.Text.Json;
using Myc.Core.Sorting;
using System.Text.Json.Serialization;

namespace Myc.Core.Configuration;

/// <summary>
/// Reads and writes <c>~/.config/myc</c>. A broken file falls back to the defaults
/// instead of stopping the app.
/// </summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public ConfigStore(string? root = null)
    {
        Root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "myc");
    }

    public string Root { get; }

    public string StatePath => Path.Combine(Root, "state.json");

    public string SettingsPath => Path.Combine(Root, "settings.json");

    public SessionState LoadState(string leftFallback, string rightFallback)
    {
        SessionState fallback = SessionState.Default(leftFallback, rightFallback);
        SessionState? loaded = Read<SessionState>(StatePath);
        if (loaded?.Left is null || loaded.Right is null)
        {
            return fallback;
        }

        return new SessionState
        {
            Left = Normalize(loaded.Left, fallback.Left),
            Right = Normalize(loaded.Right, fallback.Right),
            Active = loaded.Active == SessionState.RightPanel ? SessionState.RightPanel : SessionState.LeftPanel,
        };
    }

    public void SaveState(SessionState state) => Write(StatePath, state);

    public AppSettings LoadSettings() => Read<AppSettings>(SettingsPath)?.Normalized() ?? AppSettings.Default;

    public void SaveSettings(AppSettings settings) => Write(SettingsPath, settings.Normalized());

    /// <summary>Writes the default settings the first time, and leaves an existing file alone.</summary>
    public AppSettings EnsureSettings()
    {
        if (File.Exists(SettingsPath))
        {
            return LoadSettings();
        }

        AppSettings settings = AppSettings.Default;
        SaveSettings(settings);
        return settings;
    }

    private static PanelState Normalize(PanelState panel, PanelState fallback)
    {
        string directory = string.IsNullOrWhiteSpace(panel.Directory) ? fallback.Directory : panel.Directory;
        string sort = PanelSort.Normalize(panel.Sort);
        return panel with { Directory = directory, Sort = sort };
    }

    private static T? Read<T>(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return default;
        }
    }

    private void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Root);
        string json = JsonSerializer.Serialize(value, Json);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, json + Environment.NewLine);
        File.Move(temporary, path, overwrite: true);
    }
}
