using System.Globalization;
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

        Assert.Equal("2026-10-04 11:02", EntryText.FormatListDate(modified));
        Assert.Equal("2026-10-04 11:02", EntryText.FormatDetailDate(modified));
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

    [Fact]
    public void A_combining_accent_is_kept_with_its_letter()
    {
        string shown = EntryText.EllipsizeMiddle("cafe\u0301-very-long-name.txt", 14, Columns);

        Assert.True(Columns(shown) <= 14);
        Assert.EndsWith(".txt", shown);
        Assert.DoesNotContain(Graphemes(shown), element => char.GetUnicodeCategory(element[0]) == UnicodeCategory.NonSpacingMark);
    }

    [Fact]
    public void A_wide_character_is_not_split()
    {
        string shown = EntryText.EllipsizeMiddle("\u6587\u4ef6\u6587\u4ef6\u6587\u4ef6.txt", 8, Columns);

        Assert.True(Columns(shown) <= 8);
        Assert.EndsWith(".txt", shown);
    }

    [Theory]
    [InlineData(80, true, true)]
    [InlineData(40, true, true)]
    [InlineData(28, true, false)]
    [InlineData(19, false, false)]
    public void Narrow_panels_drop_modified_before_size(int width, bool size, bool modified)
    {
        PanelColumns columns = PanelColumns.For(width);

        Assert.Equal(size, columns.ShowSize);
        Assert.Equal(modified, columns.ShowModified);
        Assert.True(columns.Name >= 12 || width < 12);
    }

    private static int Columns(string text)
    {
        int width = 0;
        foreach (string element in Graphemes(text))
        {
            int rune = char.ConvertToUtf32(element, 0);
            width += rune is >= 0x1100 and <= 0x115F
                or >= 0x2E80 and <= 0xA4CF
                or >= 0xAC00 and <= 0xD7A3
                or >= 0xF900 and <= 0xFAFF
                or >= 0xFE10 and <= 0xFE19
                or >= 0xFE30 and <= 0xFE6F
                or >= 0xFF00 and <= 0xFF60
                or >= 0xFFE0 and <= 0xFFE6
                or >= 0x1F300 and <= 0x1FAFF
                ? 2
                : 1;
        }

        return width;
    }

    private static IEnumerable<string> Graphemes(string text)
    {
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            yield return enumerator.GetTextElement();
        }
    }
}
