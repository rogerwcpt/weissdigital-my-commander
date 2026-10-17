namespace Myc.Core.FileSystem;

/// <summary>Prefix search for type-to-jump. The caller owns the timeout and the cursor.</summary>
public static class NameJump
{
    /// <summary>
    /// Index of the first name that starts with <paramref name="query"/>, skipping <c>.</c> and <c>..</c>.
    /// Comparison is case-insensitive and uses composed characters, so a typed <c>é</c> matches a decomposed name.
    /// </summary>
    public static int Find(IReadOnlyList<string> names, string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return -1;
        }

        string needle = EntryNames.Compose(query);
        for (int index = 0; index < names.Count; index++)
        {
            string name = names[index];
            if (name is "." or "..")
            {
                continue;
            }

            if (EntryNames.Compose(name).StartsWith(needle, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}
