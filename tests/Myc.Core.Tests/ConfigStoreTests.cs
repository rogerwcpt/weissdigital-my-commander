using Myc.Core.Configuration;

namespace Myc.Core.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("myc-").FullName;

    [Fact]
    public void State_round_trips_both_panels()
    {
        var store = new ConfigStore(_root);
        SessionState state = SessionState.Default("/tmp/left", "/tmp/right") with
        {
            Active = SessionState.RightPanel,
            Left = PanelState.Default("/tmp/left") with { ShowHidden = false, Descending = true },
            Right = PanelState.Default("/tmp/right") with { Sort = "size" },
        };

        store.SaveState(state);
        SessionState loaded = store.LoadState("/fallback/left", "/fallback/right");

        Assert.Equal(state, loaded);
        Assert.False(File.Exists(store.StatePath + ".tmp"));
    }

    [Fact]
    public void An_unknown_sort_name_comes_back_as_name()
    {
        var store = new ConfigStore(_root);
        store.SaveState(SessionState.Default("/tmp/left", "/tmp/right") with
        {
            Left = PanelState.Default("/tmp/left") with { Sort = "nope" },
        });

        Assert.Equal("name", store.LoadState("/tmp/left", "/tmp/right").Left.Sort);
    }

    [Fact]
    public void A_missing_or_broken_file_uses_the_defaults()
    {
        var store = new ConfigStore(_root);

        Assert.Equal(SessionState.Default("/left", "/right"), store.LoadState("/left", "/right"));
        Assert.Equal(AppSettings.Default, store.LoadSettings());

        Directory.CreateDirectory(_root);
        File.WriteAllText(store.StatePath, "{");
        File.WriteAllText(store.SettingsPath, "nope");

        Assert.Equal(SessionState.Default("/left", "/right"), store.LoadState("/left", "/right"));
        Assert.Equal(AppSettings.Default, store.LoadSettings());
    }

    [Fact]
    public void Ensure_settings_writes_once_and_keeps_a_later_edit()
    {
        var store = new ConfigStore(_root);

        AppSettings first = store.EnsureSettings();
        store.SaveSettings(AppSettings.Default with { Theme = "Paper", Startup = "default" });
        AppSettings second = store.EnsureSettings();

        Assert.Equal(AppSettings.Default, first);
        Assert.Equal("Paper", second.Theme);
        Assert.Equal(AppSettings.DefaultStartup, second.Startup);
    }

    [Fact]
    public void A_theme_keeps_its_spelling_and_an_unknown_one_is_graphite()
    {
        var store = new ConfigStore(_root);

        store.SaveSettings(AppSettings.Default with { Theme = "amber phosphor" });
        Assert.Equal("Amber Phosphor", store.LoadSettings().Theme);

        store.SaveSettings(AppSettings.Default with { Theme = "nope" });
        Assert.Equal(AppSettings.DefaultTheme, store.LoadSettings().Theme);
    }

    [Fact]
    public void A_missing_directory_walks_up_to_a_parent_that_exists()
    {
        string parent = Path.Combine(_root, "kept");
        Directory.CreateDirectory(parent);
        string gone = Path.Combine(parent, "removed", "deeper");

        Assert.Equal(parent, SessionPaths.ResolveDirectory(gone, "/unused"));
        Assert.Equal(parent, SessionPaths.ResolveDirectory("\0", parent));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
