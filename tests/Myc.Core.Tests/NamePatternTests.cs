using Myc.Core.FileSystem;
using Myc.Core.Selection;

namespace Myc.Core.Tests;

public class NamePatternTests
{
    [Theory]
    [InlineData("*.jpg", "photo.jpg", true)]
    [InlineData("*.jpg", "Photo.JPG", true)]
    [InlineData("*.jpg", "readme.md", false)]
    [InlineData("*.jpg", "archive.jpeg", false)]
    [InlineData("file?.txt", "file1.txt", true)]
    [InlineData("file?.txt", "fileA.txt", true)]
    [InlineData("file?.txt", "file.txt", false)]
    [InlineData("file?.txt", "file10.txt", false)]
    [InlineData("readme", "README", true)]
    [InlineData("readme", "readme.md", false)]
    [InlineData("*", "folder", true)]
    [InlineData("*", ".gitignore", true)]
    [InlineData("*.*", "photo.jpg", true)]
    [InlineData("*.*", "folder", false)]
    [InlineData("", "photo.jpg", false)]
    [InlineData("   ", "photo.jpg", false)]
    [InlineData(" *.jpg ", "photo.jpg", true)]
    public void A_pattern_matches_the_whole_name(string pattern, string name, bool expected)
    {
        Assert.Equal(expected, NamePattern.Matches(pattern, name));
    }

    [Fact]
    public void A_composed_pattern_matches_a_decomposed_name()
    {
        string decomposed = "cafe\u0301-notes.txt";

        Assert.True(NamePattern.Matches("café*", decomposed));
    }

    [Fact]
    public void The_parent_row_is_not_a_match()
    {
        FileEntry[] entries =
        [
            Row("..", parent: true),
            Row("photo.jpg"),
            Row("notes.txt"),
            Row("Photos", directory: true),
        ];

        Assert.Equal(1, NamePattern.Count(entries, "*.jpg"));
        Assert.Equal(3, NamePattern.Count(entries, "*"));
        Assert.Equal(0, NamePattern.Count(entries, ".."));
    }

    private static FileEntry Row(string name, bool parent = false, bool directory = false) => new()
    {
        Name = name,
        FullPath = "/" + name,
        Kind = directory || parent ? FileKind.Directory : FileKind.File,
        IsParent = parent,
    };
}
