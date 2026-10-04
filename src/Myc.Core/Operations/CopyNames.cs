namespace Myc.Core.Operations;

/// <summary>Names used when a copy would land on an existing item.</summary>
public static class CopyNames
{
    /// <summary><c>notes.md</c> becomes <c>notes copy.md</c>. Dotfiles gain a suffix and no fake extension.</summary>
    public static string Duplicate(string name)
    {
        int dot = ExtensionDot(name);
        return dot < 0 ? name + " copy" : string.Concat(name.AsSpan(0, dot), " copy", name.AsSpan(dot));
    }

    /// <summary><c>notes.md</c> and <c>2</c> become <c>notes (2).md</c>.</summary>
    public static string Numbered(string name, int number)
    {
        int dot = ExtensionDot(name);
        string mark = " (" + number + ")";
        return dot < 0 ? name + mark : string.Concat(name.AsSpan(0, dot), mark, name.AsSpan(dot));
    }

    public static string NextNumbered(string name, Func<string, bool> exists)
    {
        for (int number = 2; number < 10_000; number++)
        {
            string candidate = Numbered(name, number);
            if (!exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("No free name.");
    }

    private static int ExtensionDot(string name)
    {
        int dot = name.LastIndexOf('.');
        return dot > 0 && dot < name.Length - 1 ? dot : -1;
    }
}
