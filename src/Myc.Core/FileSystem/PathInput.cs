namespace Myc.Core.FileSystem;

/// <summary>One name Tab can fill in. Directories gain a trailing slash when they are the only match.</summary>
public readonly record struct PathMatch(string Name, bool IsDirectory);

/// <summary>The text a go-to field holds: <c>~</c>, a relative fragment, and Tab completion.</summary>
public static class PathInput
{
    public static string? Resolve(string text, string currentDirectory, string home)
    {
        string trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        string expanded = trimmed == "~"
            ? home
            : trimmed.StartsWith("~/", StringComparison.Ordinal) || trimmed.StartsWith("~\\", StringComparison.Ordinal)
                ? Path.Combine(home, trimmed[2..])
                : trimmed;
        try
        {
            string rooted = Path.IsPathRooted(expanded)
                ? expanded
                : Path.Combine(currentDirectory, expanded);
            return Path.GetFullPath(rooted);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// Replaces the last path segment from <paramref name="names"/>. A single match is filled in,
    /// and a directory gets a trailing slash. Several matches share the longest prefix.
    /// <c>~</c> becomes <c>~/</c>.
    /// </summary>
    public static string Complete(string text, IReadOnlyList<PathMatch> names)
    {
        string trimmed = text.Trim();
        if (trimmed == "~")
        {
            return "~/";
        }

        (string directoryText, string fragment) = Split(trimmed);
        List<PathMatch> matches = names
            .Where(name => name.Name is not "." and not ".." && StartsWith(name.Name, fragment))
            .OrderBy(name => name.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (matches.Count == 0 || (fragment.Length == 0 && matches.Count > 1))
        {
            return trimmed;
        }

        if (matches.Count == 1)
        {
            PathMatch match = matches[0];
            string shown = match.IsDirectory ? match.Name + "/" : match.Name;
            return directoryText + shown;
        }

        string common = CommonPrefix(matches);
        return common.Length > fragment.Length ? directoryText + common : trimmed;
    }

    /// <summary>The directory to list, and the segment still being typed. The directory text keeps its slash.</summary>
    public static (string DirectoryText, string Fragment) Split(string text)
    {
        int slash = Math.Max(text.LastIndexOf('/'), text.LastIndexOf('\\'));
        if (slash < 0)
        {
            return ("", text);
        }

        return (text[..(slash + 1)], text[(slash + 1)..]);
    }

    private static bool StartsWith(string name, string fragment)
    {
        if (fragment.Length == 0)
        {
            return true;
        }

        return EntryNames.Compose(name).StartsWith(EntryNames.Compose(fragment), StringComparison.OrdinalIgnoreCase);
    }

    private static string CommonPrefix(List<PathMatch> matches)
    {
        string prefix = matches[0].Name;
        for (int i = 1; i < matches.Count && prefix.Length > 0; i++)
        {
            string name = matches[i].Name;
            int length = 0;
            int max = Math.Min(prefix.Length, name.Length);
            while (length < max && char.ToUpperInvariant(prefix[length]) == char.ToUpperInvariant(name[length]))
            {
                length++;
            }

            prefix = prefix[..length];
        }

        return prefix;
    }
}
