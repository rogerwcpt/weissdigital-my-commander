using System.Text;
using Myc.Core.FileSystem;

namespace Myc.Core.Tests;

public class NameJumpTests
{
    private static readonly string[] Names = ["..", "README.md", "notes.txt", "Pi", "src", ".gitignore"];

    [Fact]
    public void An_empty_query_matches_nothing()
    {
        Assert.Equal(-1, NameJump.Find(Names, ""));
    }

    [Fact]
    public void The_first_prefix_wins_and_the_parent_row_is_skipped()
    {
        Assert.Equal(1, NameJump.Find(Names, "r"));
        Assert.Equal(2, NameJump.Find(Names, "no"));
        Assert.Equal(3, NameJump.Find(Names, "p"));
    }

    [Fact]
    public void Matching_ignores_case()
    {
        Assert.Equal(1, NameJump.Find(Names, "readme"));
        Assert.Equal(3, NameJump.Find(Names, "PI"));
    }

    [Fact]
    public void A_dot_matches_a_hidden_name()
    {
        Assert.Equal(5, NameJump.Find(Names, ".g"));
    }

    [Fact]
    public void A_miss_returns_minus_one()
    {
        Assert.Equal(-1, NameJump.Find(Names, "zzz"));
    }

    [Fact]
    public void A_composed_query_matches_a_decomposed_name()
    {
        string decomposed = "cafe\u0301.txt".Normalize(NormalizationForm.FormD);
        int index = NameJump.Find(["..", decomposed], "café");

        Assert.Equal(1, index);
    }
}
