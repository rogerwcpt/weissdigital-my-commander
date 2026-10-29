using Myc.Core.Operations;

namespace Myc.Core.Tests;

public class CopyNameTests
{
    [Theory]
    [InlineData("notes.md", "notes copy.md")]
    [InlineData("archive.tar.gz", "archive.tar copy.gz")]
    [InlineData("folder", "folder copy")]
    [InlineData(".gitignore", ".gitignore copy")]
    public void Duplicate_keeps_the_last_extension(string name, string expected)
    {
        Assert.Equal(expected, CopyNames.Duplicate(name));
    }

    [Theory]
    [InlineData("notes.md", 2, "notes (2).md")]
    [InlineData("archive.tar.gz", 3, "archive.tar (3).gz")]
    [InlineData(".gitignore", 2, ".gitignore (2)")]
    public void Numbered_keeps_the_last_extension(string name, int number, string expected)
    {
        Assert.Equal(expected, CopyNames.Numbered(name, number));
    }

    [Fact]
    public void Next_numbered_skips_names_that_are_taken()
    {
        string next = CopyNames.NextNumbered("notes.md", name => name is "notes (2).md");

        Assert.Equal("notes (3).md", next);
    }

    [Fact]
    public void Next_duplicate_walks_to_a_free_name()
    {
        Assert.Equal("notes copy.md", CopyNames.NextDuplicate("notes.md", _ => false));
        Assert.Equal("notes copy 2.md", CopyNames.NextDuplicate("notes.md", name => name == "notes copy.md"));
        Assert.Equal(
            "notes copy 3.md",
            CopyNames.NextDuplicate("notes.md", name => name is "notes copy.md" or "notes copy 2.md"));
        Assert.Equal(".gitignore copy 2", CopyNames.NextDuplicate(".gitignore", name => name == ".gitignore copy"));
    }
}
