using Myc.Core.FileSystem;

namespace Myc.Core.Tests;

public class DirectoryNavigationTests
{
    [Fact]
    public void Entering_a_directory_starts_at_the_first_entry()
    {
        DirectoryMove? move = DirectoryNavigation.Enter(Entry("src", isContainer: true), "/work/myc");

        Assert.Equal(new DirectoryMove("/work/myc/src", null), move);
    }

    [Fact]
    public void Entering_a_file_does_not_move()
    {
        Assert.Null(DirectoryNavigation.Enter(Entry("notes.md", isContainer: false), "/work/myc"));
    }

    [Fact]
    public void Going_up_selects_the_folder_that_was_left()
    {
        DirectoryMove? move = DirectoryNavigation.Up("/work/myc");

        Assert.Equal(new DirectoryMove("/work", "myc"), move);
    }

    [Fact]
    public void Parent_row_goes_up_the_same_way()
    {
        var parent = new FileEntry
        {
            Name = "..",
            FullPath = "/work",
            Kind = FileKind.Directory,
            IsParent = true,
        };

        Assert.Equal(DirectoryNavigation.Up("/work/myc"), DirectoryNavigation.Enter(parent, "/work/myc"));
    }

    [Fact]
    public void Root_has_no_parent()
    {
        Assert.Null(DirectoryNavigation.Up("/"));
    }

    private static FileEntry Entry(string name, bool isContainer) => new()
    {
        Name = name,
        FullPath = "/work/myc/" + name,
        Kind = isContainer ? FileKind.Directory : FileKind.File,
    };
}
