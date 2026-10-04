using Myc.Core.FileSystem;
using Myc.Core.Selection;

namespace Myc.Core.Tests;

public class EffectiveSelectionTests
{
    private static readonly FileEntry Parent = Entry("..", isParent: true);
    private static readonly FileEntry Alpha = Entry("alpha");
    private static readonly FileEntry Beta = Entry("beta");
    private static readonly IReadOnlyList<FileEntry> Entries = [Parent, Alpha, Beta];

    [Fact]
    public void No_marks_uses_the_cursor_entry()
    {
        IReadOnlyList<FileEntry> selected = EffectiveSelection.Resolve(Entries, new HashSet<string>(), cursorIndex: 2);

        Assert.Equal([Beta], selected);
    }

    [Fact]
    public void Marks_win_over_the_cursor_and_keep_panel_order()
    {
        IReadOnlyList<FileEntry> selected = EffectiveSelection.Resolve(
            Entries,
            new HashSet<string> { "beta", "alpha" },
            cursorIndex: 2);

        Assert.Equal([Alpha, Beta], selected);
    }

    [Fact]
    public void Parent_is_never_selected()
    {
        // A mark on .. does not count. With no other marks, the cursor entry is used.
        Assert.Equal([Alpha], EffectiveSelection.Resolve(Entries, new HashSet<string> { ".." }, cursorIndex: 1));
        Assert.Empty(EffectiveSelection.Resolve(Entries, new HashSet<string> { ".." }, cursorIndex: 0));
        Assert.Empty(EffectiveSelection.Resolve(Entries, new HashSet<string>(), cursorIndex: 0));
    }

    [Fact]
    public void Cursor_past_the_end_selects_nothing_when_unmarked()
    {
        Assert.Empty(EffectiveSelection.Resolve(Entries, new HashSet<string>(), cursorIndex: 9));
    }

    private static FileEntry Entry(string name, bool isParent = false) => new()
    {
        Name = name,
        FullPath = "/" + name,
        Kind = isParent ? FileKind.Directory : FileKind.File,
        IsParent = isParent,
    };
}
