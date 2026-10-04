using Myc.Core.Display;

namespace Myc.Core.Tests;

public class EntryTextTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(2048, "2 KB")]
    [InlineData(219136, "214 KB")]
    [InlineData(4299162, "4.1 MB")]
    public void Sizes_use_finder_style_units(long bytes, string expected)
    {
        Assert.Equal(expected, EntryText.FormatSize(bytes));
    }

    [Fact]
    public void Dates_are_day_month_and_a_fuller_footer_form()
    {
        var modified = new DateTimeOffset(2026, 10, 4, 11, 2, 0, TimeSpan.Zero);

        Assert.Equal("04 Oct", EntryText.FormatListDate(modified));
        Assert.Equal("04 Oct 2026 11:02", EntryText.FormatDetailDate(modified));
    }

    [Fact]
    public void Long_names_keep_the_extension()
    {
        string shown = EntryText.EllipsizeMiddle("very-long-filename.txt", 18, text => text.Length);

        Assert.EndsWith(".txt", shown);
        Assert.Contains("…", shown);
        Assert.True(shown.Length <= 18);
    }

    [Fact]
    public void Short_names_are_unchanged()
    {
        Assert.Equal("notes.md", EntryText.EllipsizeMiddle("notes.md", 18, text => text.Length));
    }
}
