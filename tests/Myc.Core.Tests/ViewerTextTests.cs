using Myc.Core.FileSystem;

namespace Myc.Core.Tests;

public class ViewerTextTests
{
    [Theory]
    [InlineData("notes.md", true)]
    [InlineData("Notes.MD", true)]
    [InlineData("post.markdown", true)]
    [InlineData("draft.mdown", true)]
    [InlineData("readme.txt", false)]
    [InlineData("notes.md.bak", false)]
    [InlineData("README", false)]
    public void Markdown_is_the_extension(string name, bool expected)
    {
        Assert.Equal(expected, ViewerText.LooksLikeMarkdown(name));
    }

    [Fact]
    public void A_text_file_is_read()
    {
        string path = Write("hello\nworld");
        try
        {
            ViewerText.Content content = ViewerText.Read(path);

            Assert.True(content.Ok);
            Assert.Equal("hello\nworld", content.Text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_nul_in_the_first_8_kb_is_not_text()
    {
        string path = Write("abc\0def");
        try
        {
            ViewerText.Content content = ViewerText.Read(path);

            Assert.False(content.Ok);
            Assert.Equal("This is not a text file.", content.Error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_file_over_1_mb_is_refused()
    {
        string path = Path.Combine(Path.GetTempPath(), "myc-view-" + Guid.NewGuid().ToString("n") + ".txt");
        try
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                stream.SetLength(ViewerText.MaxBytes + 1);
            }

            ViewerText.Content content = ViewerText.Read(path);

            Assert.False(content.Ok);
            Assert.Equal("This file is larger than 1 MB.", content.Error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_missing_file_is_refused()
    {
        ViewerText.Content content = ViewerText.Read(Path.Combine(Path.GetTempPath(), "myc-view-missing-" + Guid.NewGuid().ToString("n")));

        Assert.False(content.Ok);
        Assert.Equal("No such file.", content.Error);
    }

    private static string Write(string text)
    {
        string path = Path.Combine(Path.GetTempPath(), "myc-view-" + Guid.NewGuid().ToString("n") + ".txt");
        File.WriteAllText(path, text);
        return path;
    }
}
