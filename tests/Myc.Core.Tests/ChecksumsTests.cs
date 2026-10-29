using System.Text;
using Myc.Core.Inspect;

namespace Myc.Core.Tests;

public class ChecksumsTests
{
    [Fact]
    public void Sha256_of_hello_matches_the_known_hash()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", Checksums.Sha256(stream));
    }

    [Fact]
    public void A_selection_lists_each_name()
    {
        string text = Checksums.Format(
        [
            new Checksums.Row("notes.md", "abc", null),
            new Checksums.Row("photos", null, "Not a file."),
        ]);

        Assert.Equal("notes.md\nabc\n\nphotos\nNot a file.", text);
    }
}
