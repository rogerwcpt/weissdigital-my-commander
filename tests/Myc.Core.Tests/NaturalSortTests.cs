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

    private static FileEntry Entry(
        string name,
        FileKind kind,
        bool isParent = false,
        bool pointsAtDirectory = false) => new()
    {
        Name = name,
        FullPath = "/" + name,
        Kind = kind,
        IsParent = isParent,
        PointsAtDirectory = pointsAtDirectory,
    };
}
