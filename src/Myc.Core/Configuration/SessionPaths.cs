namespace Myc.Core.Configuration;

/// <summary>Turns a remembered directory into one that still exists.</summary>
public static class SessionPaths
{
    public static string ResolveDirectory(string? saved, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(saved))
        {
            string? existing = NearestExisting(saved);
            if (existing is not null)
            {
                return existing;
            }
        }

        return NearestExisting(fallback) ?? fallback;
    }

    private static string? NearestExisting(string path)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        while (!Directory.Exists(full))
        {
            string? parent = Path.GetDirectoryName(full);
            if (string.IsNullOrEmpty(parent) || parent == full)
            {
                return null;
            }

            full = parent;
        }

        return full;
    }
}
