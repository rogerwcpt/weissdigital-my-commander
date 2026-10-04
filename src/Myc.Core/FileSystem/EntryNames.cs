using System.Globalization;
using System.Text;

namespace Myc.Core.FileSystem;

/// <summary>Name checks shared by rename and mkdir. Nothing here touches the disk.</summary>
public static class EntryNames
{
    public static string? ComponentRejection(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Enter a name.";
        }

        if (name is "." or "..")
        {
            return "That name is reserved.";
        }

        if (name.IndexOfAny(['/', '\\', '\0']) >= 0)
        {
            return "A name can't contain /.";
        }

        return null;
    }

    /// <summary>A folder name inside the current directory. Slashes create intermediate folders.</summary>
    public static string? RelativeDirectoryRejection(string name)
    {
        string trimmed = name.Trim().TrimEnd('/');
        if (trimmed.Length == 0)
        {
            return "Enter a name.";
        }

        if (trimmed.Contains('\\') || Path.IsPathRooted(trimmed))
        {
            return "Use a name inside this folder.";
        }

        foreach (string part in trimmed.Split('/'))
        {
            if (ComponentRejection(part) is { } rejection)
            {
                return rejection;
            }
        }

        return null;
    }

    /// <summary>
    /// How many leading graphemes to preselect. Finder selects the base name and leaves the
    /// last extension alone. Dotfiles, names with no dot, and a trailing dot stay fully selected.
    /// </summary>
    public static int BasenameGraphemes(string name)
    {
        int dot = name.LastIndexOf('.');
        string selected = dot > 0 && dot < name.Length - 1 ? name[..dot] : name;
        return GraphemeCount(selected);
    }

    /// <summary>NFC form, so a typed <c>é</c> matches the decomposed name macOS stores.</summary>
    public static string Compose(string name) => name.Normalize(NormalizationForm.FormC);

    public static bool Same(string? left, string? right) =>
        left is not null
        && right is not null
        && string.Equals(Compose(left), Compose(right), StringComparison.Ordinal);

    public static bool EqualIgnoringCase(string? left, string? right) =>
        left is not null
        && right is not null
        && string.Equals(Compose(left), Compose(right), StringComparison.OrdinalIgnoreCase);

    public static int GraphemeCount(string value)
    {
        int count = 0;
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }
}

/// <summary>Mark-set equality. Names that look the same after composition are the same mark.</summary>
public sealed class FileNameComparer : IEqualityComparer<string>
{
    public static FileNameComparer Ordinal { get; } = new();

    public bool Equals(string? x, string? y) => EntryNames.Same(x, y);

    public int GetHashCode(string obj) => string.GetHashCode(EntryNames.Compose(obj), StringComparison.Ordinal);
}
