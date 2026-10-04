using Myc.Core.FileSystem;

namespace Myc.Core.Tests;

public class PathInputTests
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "myc-path-home");
    private readonly string _current;

    public PathInputTests()
    {
        _current = Path.Combine(_home, "work");
    }

    [Fact]
    public void A_tilde_is_the_home_directory()
    {
        Assert.Equal(_home, PathInput.Resolve("~", _current, _home));
        Assert.Equal(Path.Combine(_home, "Documents"), PathInput.Resolve("~/Documents", _current, _home));
    }

    [Fact]
    public void A_relative_path_starts_from_the_current_directory()
    {
        Assert.Equal(Path.Combine(_current, "notes"), PathInput.Resolve("notes", _current, _home));
        Assert.Equal(_home, PathInput.Resolve("..", _current, _home));
    }

    [Fact]
    public void Blank_text_does_not_resolve()
    {
        Assert.Null(PathInput.Resolve("   ", _current, _home));
    }

    [Fact]
    public void Tab_turns_a_bare_tilde_into_a_slash()
    {
        Assert.Equal("~/", PathInput.Complete("~", []));
    }

    [Fact]
    public void Tab_fills_the_only_matching_directory_and_steps_inside()
    {
        PathMatch[] names = [new("Documents", true), new("notes.txt", false)];

        Assert.Equal("~/Documents/", PathInput.Complete("~/doc", names));
        Assert.Equal("notes.txt", PathInput.Complete("notes.t", names));
    }

    [Fact]
    public void Tab_stops_at_the_shared_prefix_when_several_names_match()
    {
        PathMatch[] names = [new("Documents", true), new("Downloads", true), new("notes.txt", false)];

        Assert.Equal("~/Do", PathInput.Complete("~/d", names));
        Assert.Equal("~/Do", PathInput.Complete("~/Do", names));
    }

    [Fact]
    public void Tab_on_a_directory_slash_does_not_replace_it_with_the_first_child()
    {
        PathMatch[] names = [new("a", true), new("b", false)];

        Assert.Equal("~/", PathInput.Complete("~/", names));
    }

    [Fact]
    public void A_composed_fragment_matches_a_decomposed_name()
    {
        PathMatch[] names = [new("cafe\u0301-notes", true)];

        Assert.Equal("cafe\u0301-notes/", PathInput.Complete("café", names));
    }
}
