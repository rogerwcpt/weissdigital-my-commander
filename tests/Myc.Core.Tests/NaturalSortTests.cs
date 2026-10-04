using Myc.Core.FileSystem;
using Myc.Core.Sorting;

namespace Myc.Core.Tests;

public class NaturalSortTests
{
    [Theory]
    [InlineData("file2", "file10", -1)]
    [InlineData("file10", "file2", 1)]
    [InlineData("file2", "file02", -1)]
    [InlineData("a", "B", -1)]
    [InlineData("A", "a", -1)]
    [InlineData("img1.png", "img1.txt", -1)]
    [InlineData("file", "file2", -1)]
    public void Names_sort_naturally(string left, string right, int expectedSign)
    {
        int compared = NaturalNameComparer.Instance.Compare(left, right);
        Assert.Equal(expectedSign, Math.Sign(compared));
    }

    [Fact]
    public void Composed_and_decomposed_accents_sort_as_the_same_name()
    {
        Assert.Equal(0, NaturalNameComparer.Instance.Compare("caf\u00e9", "cafe\u0301"));
    }

    [Fact]
    public void Entries_put_parent_then_directories_then_files()
    {
        FileEntry[] entries =
        [
            Entry("file10.txt", FileKind.File),
            Entry("b", FileKind.Directory),
            Entry("file2.txt", FileKind.File),
            Entry("..", FileKind.Directory, isParent: true),
            Entry("a", FileKind.Directory),
            Entry("link-dir", FileKind.Symlink, pointsAtDirectory: true),
        ];

        string[] names = EntrySorter.Sort(entries).Select(entry => entry.Name).ToArray();

        Assert.Equal(["..", "a", "b", "link-dir", "file2.txt", "file10.txt"], names);
    }

    [Fact]
    public void Repeating_name_order_reverses_inside_each_group()
    {
        FileEntry[] entries =
        [
            Entry("file2.txt", FileKind.File),
            Entry("b", FileKind.Directory),
            Entry("file10.txt", FileKind.File),
            Entry("..", FileKind.Directory, isParent: true),
            Entry("a", FileKind.Directory),
        ];

        string[] names = EntrySorter.Sort(entries, PanelSort.Name, descending: true).Select(entry => entry.Name).ToArray();

        Assert.Equal(["..", "b", "a", "file10.txt", "file2.txt"], names);
    }

    [Fact]
    public void Extension_order_uses_the_last_suffix_and_ignores_case()
    {
        FileEntry[] entries =
        [
            Entry("notes.txt", FileKind.File),
            Entry("readme.md", FileKind.File),
            Entry("Archive.MD", FileKind.File),
            Entry(".gitignore", FileKind.File),
            Entry("photo.jpeg", FileKind.File),
            Entry("..", FileKind.Directory, isParent: true),
            Entry("folder", FileKind.Directory),
            Entry("Widget.app", FileKind.Directory),
        ];

        string[] names = EntrySorter.Sort(entries, PanelSort.Extension).Select(entry => entry.Name).ToArray();

        Assert.Equal(
            ["..", "folder", "Widget.app", ".gitignore", "photo.jpeg", "Archive.MD", "readme.md", "notes.txt"],
            names);
    }

    [Fact]
    public void Size_order_keeps_directories_first_and_breaks_ties_by_name()
    {
        FileEntry[] entries =
        [
            Entry("z", FileKind.File, size: 9),
            Entry("b", FileKind.File, size: 5),
            Entry("a", FileKind.File, size: 5),
            Entry("unknown", FileKind.File),
            Entry("..", FileKind.Directory, isParent: true),
            Entry("dir", FileKind.Directory),
        ];

        Assert.Equal(
            ["..", "dir", "unknown", "a", "b", "z"],
            EntrySorter.Sort(entries, PanelSort.Size).Select(entry => entry.Name).ToArray());
        Assert.Equal(
            ["..", "dir", "z", "a", "b", "unknown"],
            EntrySorter.Sort(entries, PanelSort.Size, descending: true).Select(entry => entry.Name).ToArray());
    }

    [Fact]
    public void Modified_order_is_oldest_first_until_reversed()
    {
        DateTimeOffset older = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset newer = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        FileEntry[] entries =
        [
            Entry("new", FileKind.File, modified: newer),
            Entry("old", FileKind.File, modified: older),
            Entry("..", FileKind.Directory, isParent: true),
        ];

        Assert.Equal(["..", "old", "new"], EntrySorter.Sort(entries, PanelSort.Modified).Select(entry => entry.Name).ToArray());
        Assert.Equal(["..", "new", "old"], EntrySorter.Sort(entries, PanelSort.Modified, descending: true).Select(entry => entry.Name).ToArray());
    }

    [Fact]
    public void An_unknown_order_sorts_by_name()
    {
        FileEntry[] entries = [Entry("b", FileKind.File), Entry("a", FileKind.File)];

        Assert.Equal(
            ["a", "b"],
            EntrySorter.Sort(entries, "nope").Select(entry => entry.Name).ToArray());
    }

    [Fact]
    public void The_heading_marks_the_column_that_is_in_use()
    {
        Assert.Equal("Name ▲", PanelSort.Headers(PanelSort.Name, descending: false, showSize: true, showModified: true).Name);
        Assert.Equal("Name ▼", PanelSort.Headers(PanelSort.Name, descending: true, showSize: true, showModified: true).Name);
        Assert.Equal("Size ▲", PanelSort.Headers(PanelSort.Size, descending: false, showSize: true, showModified: true).Size);
        Assert.Equal("Name", PanelSort.Headers(PanelSort.Size, descending: false, showSize: true, showModified: true).Name);
        Assert.Equal("Size ▲", PanelSort.Headers(PanelSort.Size, descending: false, showSize: false, showModified: false).Name);
        Assert.Equal("Modified ▼", PanelSort.Headers(PanelSort.Modified, descending: true, showSize: true, showModified: true).Modified);
        Assert.Equal("Extension ▲", PanelSort.Headers(PanelSort.Extension, descending: false, showSize: true, showModified: true).Name);
    }

    private static FileEntry Entry(
        string name,
        FileKind kind,
        bool isParent = false,
        bool pointsAtDirectory = false,
        long? size = null,
        DateTimeOffset? modified = null) => new()
    {
        Name = name,
        FullPath = "/" + name,
        Kind = kind,
        IsParent = isParent,
        PointsAtDirectory = pointsAtDirectory,
        Size = size,
        Modified = modified,
    };
}
