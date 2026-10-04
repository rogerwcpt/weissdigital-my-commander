using System.IO.Enumeration;
using Myc.Core.FileSystem;

namespace Myc.Core.Selection;

/// <summary>
/// Shell-style names: <c>*</c> is any run of characters, <c>?</c> is one character.
/// Matching ignores case and Unicode composition, so a pattern typed as NFC still hits an NFD name.
/// </summary>
public static class NamePattern
{
    public static bool Matches(string? pattern, string name)
    {
        if (string.IsNullOrWhiteSpace(pattern) || name.Length == 0)
        {
            return false;
        }

        string expression = EntryNames.Compose(pattern.Trim());
        string composed = EntryNames.Compose(name);
        return FileSystemName.MatchesSimpleExpression(expression, composed, ignoreCase: true);
    }

    /// <summary>How many rows the pattern would mark. <c>..</c> is never a match.</summary>
    public static int Count(IEnumerable<FileEntry> entries, string? pattern)
    {
        int count = 0;
        foreach (FileEntry entry in entries)
        {
            if (!entry.IsParent && Matches(pattern, entry.Name))
            {
                count++;
            }
        }

        return count;
    }
}
