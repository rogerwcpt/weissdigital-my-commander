using Myc.Core.FileSystem;

namespace Myc.Core.Sorting;

public static class EntrySorter
{
    /// <summary>
    /// <c>..</c> first, then directories and directory symlinks, then everything else.
    /// The same order again reverses within each of those groups. Names break ties,
    /// and names themselves use <see cref="NaturalNameComparer"/>.
    /// </summary>
    public static IReadOnlyList<FileEntry> Sort(IEnumerable<FileEntry> entries, string sort = PanelSort.Name, bool descending = false)
    {
        string mode = PanelSort.Normalize(sort);
        IOrderedEnumerable<FileEntry> grouped = entries.OrderBy(entry => entry.IsParent ? 0 : entry.IsContainer ? 1 : 2);
        IOrderedEnumerable<FileEntry> ordered = mode switch
        {
            PanelSort.Extension => By(grouped, ExtensionOf, descending, StringComparer.OrdinalIgnoreCase),
            PanelSort.Size => By(grouped, entry => entry.Size ?? -1L, descending, null),
            PanelSort.Modified => By(grouped, entry => entry.Modified ?? DateTimeOffset.MinValue, descending, null),
            _ => descending
                ? grouped.ThenByDescending(entry => entry.Name, NaturalNameComparer.Instance)
                : grouped.ThenBy(entry => entry.Name, NaturalNameComparer.Instance),
        };

        if (mode != PanelSort.Name)
        {
            ordered = ordered.ThenBy(entry => entry.Name, NaturalNameComparer.Instance);
        }

        return ordered.ToArray();
    }

    private static IOrderedEnumerable<FileEntry> By<T>(
        IOrderedEnumerable<FileEntry> grouped,
        Func<FileEntry, T> key,
        bool descending,
        IComparer<T>? comparer) =>
        descending ? grouped.ThenByDescending(key, comparer) : grouped.ThenBy(key, comparer);

    /// <summary>The last suffix. <c>.gitignore</c> and <c>notes.</c> have none. <c>a.tar.gz</c> is <c>gz</c>.</summary>
    private static string ExtensionOf(FileEntry entry)
    {
        string name = entry.Name;
        int dot = name.LastIndexOf('.');
        return dot > 0 && dot < name.Length - 1 ? name[(dot + 1)..] : "";
    }
}
