using Myc.Core.FileSystem;

namespace Myc.Core.Tests;

public class EntryNameTests
{
    [Theory]
    [InlineData("notes.md", 5)]
    [InlineData("archive.tar.gz", 11)]
    [InlineData("README", 6)]
    [InlineData(".gitignore", 10)]
    [InlineData("file.", 5)]
    public void Basename_selection_leaves_the_last_extension(string name, int graphemes)
    {
        Assert.Equal(graphemes, EntryNames.BasenameGraphemes(name));
    }

    [Fact]
    public void Basename_selection_counts_nfd_as_one_grapheme()
    {
        Assert.Equal(4, EntryNames.BasenameGraphemes("cafe\u0301.txt"));
    }

    [Fact]
    public void Composed_and_decomposed_names_are_the_same_file()
    {
        Assert.True(EntryNames.Same("caf\u00e9.txt", "cafe\u0301.txt"));
        Assert.True(EntryNames.EqualIgnoringCase("CAF\u00c9.TXT", "cafe\u0301.txt"));
        Assert.False(EntryNames.Same("readme", "README"));
    }

    [Theory]
    [InlineData("", "Enter a name.")]
    [InlineData("   ", "Enter a name.")]
    [InlineData(".", "That name is reserved.")]
    [InlineData("..", "That name is reserved.")]
    [InlineData("a/b", "A name can't contain /.")]
    [InlineData("ok", null)]
    public void A_rename_rejects_an_unusable_component(string name, string? error)
    {
        Assert.Equal(error, EntryNames.ComponentRejection(name));
    }

    [Theory]
    [InlineData("a/b/c", null)]
    [InlineData("a/../b", "That name is reserved.")]
    [InlineData("/tmp/x", "Use a name inside this folder.")]
    [InlineData("a//b", "Enter a name.")]
    public void A_new_folder_may_be_nested(string name, string? error)
    {
        Assert.Equal(error, EntryNames.RelativeDirectoryRejection(name));
    }
}
