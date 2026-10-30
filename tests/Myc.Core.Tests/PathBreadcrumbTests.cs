using Myc.Core.Display;
using Myc.Core.FileSystem;

namespace Myc.Core.Tests;

public class PathBreadcrumbTests
{
    private static readonly string Root = Path.GetPathRoot(Path.GetFullPath(".")) ?? "/";
    private static readonly string Home = Path.Combine(Root, "Users", "roger");

    [Fact]
    public void A_home_path_uses_a_tilde()
    {
        string full = Path.Combine(Home, "Projects", "foo");

        BreadcrumbTitle title = Layout(full, 80);

        Assert.Equal("~/Projects/foo", title.Text);
    }

    [Fact]
    public void Clicking_a_segment_opens_it_and_selects_the_child_that_was_left()
    {
        string full = Path.Combine(Home, "Projects", "foo");
        BreadcrumbTitle title = Layout(full, 80);

        DirectoryMove home = PathBreadcrumb.Hit(title, Column(title, "~"))!.Value;
        Assert.Equal(Home, home.Directory);
        Assert.Equal("Projects", home.SelectName);

        DirectoryMove projects = PathBreadcrumb.Hit(title, Column(title, "Projects"))!.Value;
        Assert.Equal(Path.Combine(Home, "Projects"), projects.Directory);
        Assert.Equal("foo", projects.SelectName);
        Assert.Null(PathBreadcrumb.Hit(title, Column(title, "foo")));
        Assert.Null(PathBreadcrumb.Hit(title, title.Text.IndexOf('/')));
    }

    [Fact]
    public void The_open_folder_and_an_ellipsis_are_not_clicks()
    {
        string full = Path.Combine(Home, "a", "b", "c", "d");
        BreadcrumbTitle title = Layout(full, 5);

        Assert.Equal("~/…/d", title.Text);
        DirectoryMove home = PathBreadcrumb.Hit(title, 0)!.Value;
        Assert.Equal(Home, home.Directory);
        Assert.Equal("a", home.SelectName);
        Assert.Null(PathBreadcrumb.Hit(title, title.Text.IndexOf('…')));
        Assert.Null(PathBreadcrumb.Hit(title, title.Text.IndexOf('d')));
    }

    [Fact]
    public void The_root_slash_opens_the_volume()
    {
        string full = Path.Combine(Root, "Users", "foo");
        BreadcrumbTitle title = Layout(full, 80);

        Assert.StartsWith("/", title.Text);
        DirectoryMove root = PathBreadcrumb.Hit(title, 0)!.Value;
        Assert.Equal(Path.GetFullPath(Root), root.Directory);
        Assert.Equal("Users", root.SelectName);
    }

    [Fact]
    public void A_lookalike_home_prefix_stays_a_full_path()
    {
        string full = Path.Combine(Root, "Users", "roger2", "desk");

        BreadcrumbTitle title = Layout(full, 80);

        Assert.DoesNotContain("~", title.Text);
        Assert.Contains("roger2", title.Text);
    }

    [Fact]
    public void A_long_open_folder_shrinks_so_its_parent_stays_clickable()
    {
        string full = Path.Combine(Home, "Projects", "GitHub.Personal", "weissdigital-my-commander");
        BreadcrumbTitle title = Layout(full, 40);

        Assert.Contains("GitHub.Personal", title.Text);
        Assert.Contains("Projects", title.Text);
        DirectoryMove parent = PathBreadcrumb.Hit(title, Column(title, "GitHub.Personal"))!.Value;
        Assert.Equal(Path.Combine(Home, "Projects", "GitHub.Personal"), parent.Directory);
        Assert.Equal("weissdigital-my-commander", parent.SelectName);
        Assert.Null(PathBreadcrumb.Hit(title, title.Text.LastIndexOf('…')));
    }

    [Fact]
    public void Home_itself_is_only_a_tilde()
    {
        BreadcrumbTitle title = Layout(Home, 80);

        Assert.Equal("~", title.Text);
        Assert.Null(PathBreadcrumb.Hit(title, 0));
    }

    private static BreadcrumbTitle Layout(string path, int columns) =>
        PathBreadcrumb.Layout(path, Home, columns, static text => text.Length);

    private static int Column(BreadcrumbTitle title, string label)
    {
        int at = title.Text.IndexOf(label, StringComparison.Ordinal);
        Assert.True(at >= 0, label);
        return at;
    }
}
