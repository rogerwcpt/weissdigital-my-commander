using Myc.Core.FileSystem;

namespace Myc.Core.Selection;

public static class EffectiveSelection
{
    /// <summary>
    /// Marked entries, in panel order, when any exist. Otherwise the entry under the cursor.
    /// <c>..</c> is never included.
    /// </summary>
    public static IReadOnlyList<FileEntry> Resolve(
        IReadOnlyList<FileEntry> entries,
        IReadOnlySet<string> marks,
        int cursorIndex)
    {
        List<FileEntry> marked = [];
        foreach (FileEntry entry in entries)
        {
            if (!entry.IsParent && marks.Contains(entry.Name))
            {
                marked.Add(entry);
            }
        }

        if (marked.Count > 0)
        {
            return marked;
        }

        if (cursorIndex < 0 || cursorIndex >= entries.Count)
        {
            return [];
        }

        FileEntry cursor = entries[cursorIndex];
        return cursor.IsParent ? [] : [cursor];
    }
}
