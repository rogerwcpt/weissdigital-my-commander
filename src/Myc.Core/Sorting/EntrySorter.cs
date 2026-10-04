using Myc.Core.FileSystem;

namespace Myc.Core.Sorting;

public static class EntrySorter
{
    /// <summary>
    /// <c>..</c> first, then directories and directory symlinks, then everything else.
    /// Names use <see cref="NaturalNameComparer"/>.
    /// </summary>
    public static IReadOnlyList<FileEntry> Sort(IEnumerable<FileEntry> entries)
    {
        return entries
            .OrderBy(entry => entry.IsParent ? 0 : entry.IsContainer ? 1 : 2)
            .ThenBy(entry => entry.Name, NaturalNameComparer.Instance)
            .ToArray();
    }
}
