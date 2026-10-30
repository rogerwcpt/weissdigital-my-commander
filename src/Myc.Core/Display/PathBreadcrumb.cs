using System.Text;
using Myc.Core.FileSystem;

namespace Myc.Core.Display;

/// <summary>One folder in the path drawn on a panel border.</summary>
public readonly record struct BreadcrumbPart(string Label, string Directory, string? SelectName);

/// <summary>Where one part sits in the title text. The length is in columns.</summary>
public readonly record struct BreadcrumbSpan(int Start, int Length, int PartIndex);

/// <summary>The title text and the parts a click can hit.</summary>
public readonly record struct BreadcrumbTitle(
    string Text,
    IReadOnlyList<BreadcrumbSpan> Spans,
    IReadOnlyList<BreadcrumbPart> Parts);

/// <summary>
/// The path in a panel border. Home is <c>~</c>. A click on a segment opens that folder
/// and leaves the cursor on the child that was left. The last segment is the folder already open.
/// </summary>
public static class PathBreadcrumb
{
    public static BreadcrumbTitle Layout(string fullPath, string home, int columns, Func<string, int> measure)
    {
        IReadOnlyList<BreadcrumbPart> parts = Build(fullPath, home);
        if (parts.Count == 0 || columns <= 0)
        {
            return new BreadcrumbTitle("", [], parts);
        }

        // Shorten the open folder before hiding its parent. The open folder is not a click, so a long
        // name would otherwise be the only text left on a narrow panel.
        if (Pack(parts, ellipsis: false, tailStart: 0, includeFirst: true, columns, measure) is { } full)
        {
            return full;
        }

        for (int tailStart = 2; tailStart < parts.Count; tailStart++)
        {
            if (Pack(parts, ellipsis: true, tailStart, includeFirst: true, columns, measure) is { } shortened)
            {
                return shortened;
            }
        }

        for (int tailStart = parts.Count - 1; tailStart >= 0; tailStart--)
        {
            if (Pack(parts, ellipsis: true, tailStart, includeFirst: false, columns, measure) is { } tail)
            {
                return tail;
            }
        }

        return new BreadcrumbTitle(Squeeze(parts[^1].Label, columns, measure), [], parts);
    }

    /// <summary>
    /// Fits one arrangement into <paramref name="columns"/>, shortening the open folder from the right
    /// when the parents need the room.
    /// </summary>
    private static BreadcrumbTitle? Pack(
        IReadOnlyList<BreadcrumbPart> parts,
        bool ellipsis,
        int tailStart,
        bool includeFirst,
        int columns,
        Func<string, int> measure)
    {
        var display = parts.ToArray();
        int leaf = display.Length - 1;
        string original = display[leaf].Label;
        int width = measure(original);
        string previous = "";
        for (int budget = width; budget >= 1; budget--)
        {
            string label = budget >= width ? original : SqueezeStart(original, budget, measure);
            if (label == previous)
            {
                continue;
            }

            previous = label;
            display[leaf] = display[leaf] with { Label = label };
            BreadcrumbTitle title = Compose(display, ellipsis, tailStart, includeFirst, measure);
            if (measure(title.Text) <= columns)
            {
                return title;
            }
        }

        return null;
    }

    /// <summary>The segment under <paramref name="column"/>, or null when the click is a slash, an ellipsis, or the open folder.</summary>
    public static DirectoryMove? Hit(BreadcrumbTitle title, int column)
    {
        foreach (BreadcrumbSpan span in title.Spans)
        {
            if (column < span.Start || column >= span.Start + span.Length)
            {
                continue;
            }

            BreadcrumbPart part = title.Parts[span.PartIndex];
            return part.SelectName is null ? null : new DirectoryMove(part.Directory, part.SelectName);
        }

        return null;
    }

    private static BreadcrumbTitle Compose(
        IReadOnlyList<BreadcrumbPart> parts,
        bool ellipsis,
        int tailStart,
        Func<string, int> measure) =>
        Compose(parts, ellipsis, tailStart, includeFirst: true, measure);

    private static BreadcrumbTitle Compose(
        IReadOnlyList<BreadcrumbPart> parts,
        bool ellipsis,
        int tailStart,
        bool includeFirst,
        Func<string, int> measure)
    {
        var spans = new List<BreadcrumbSpan>();
        var text = new StringBuilder();

        void Add(int index)
        {
            if (text.Length > 0 && text[^1] != '/')
            {
                text.Append('/');
            }

            int start = measure(text.ToString());
            text.Append(parts[index].Label);
            spans.Add(new BreadcrumbSpan(start, measure(text.ToString()) - start, index));
        }

        if (!ellipsis)
        {
            for (int index = 0; index < parts.Count; index++)
            {
                Add(index);
            }

            return new BreadcrumbTitle(text.ToString(), spans, parts);
        }

        if (includeFirst)
        {
            Add(0);
            if (text[^1] != '/')
            {
                text.Append('/');
            }
        }

        text.Append('…');
        text.Append('/');
        for (int index = tailStart; index < parts.Count; index++)
        {
            Add(index);
        }

        return new BreadcrumbTitle(text.ToString(), spans, parts);
    }

    private static string Squeeze(string label, int columns, Func<string, int> measure)
    {
        if (columns <= 0)
        {
            return "";
        }

        if (measure(label) <= columns)
        {
            return label;
        }

        const string dots = "…";
        int dotsWidth = Math.Max(1, measure(dots));
        if (columns <= dotsWidth)
        {
            return dots;
        }

        int keep = columns - dotsWidth;
        string end = label;
        while (end.Length > 0 && measure(end) > keep)
        {
            end = end[1..];
        }

        return dots + end;
    }

    /// <summary>Keeps the start of a long folder name. The end is the part a click does not need.</summary>
    private static string SqueezeStart(string label, int columns, Func<string, int> measure)
    {
        if (columns <= 0)
        {
            return "";
        }

        if (measure(label) <= columns)
        {
            return label;
        }

        const string dots = "…";
        int dotsWidth = Math.Max(1, measure(dots));
        if (columns <= dotsWidth)
        {
            return dots;
        }

        int keep = columns - dotsWidth;
        string head = label;
        while (head.Length > 0 && measure(head) > keep)
        {
            head = head[..^1];
        }

        return head + dots;
    }

    private static IReadOnlyList<BreadcrumbPart> Build(string fullPath, string home)
    {
        if (string.IsNullOrEmpty(fullPath))
        {
            return [];
        }

        string full = Path.GetFullPath(fullPath);
        var names = new List<string>();
        string cursor = full;
        while (true)
        {
            string? parent = Path.GetDirectoryName(cursor);
            if (string.IsNullOrEmpty(parent))
            {
                break;
            }

            names.Add(Path.GetFileName(cursor));
            cursor = parent;
        }

        names.Reverse();
        string root = cursor;
        string homeFull = string.IsNullOrEmpty(home) ? "" : Path.GetFullPath(home);
        if (IsInside(full, homeFull, out int homeCount))
        {
            if (names.Count == homeCount)
            {
                return [new BreadcrumbPart("~", homeFull, null)];
            }

            var parts = new List<BreadcrumbPart> { new("~", homeFull, names[homeCount]) };
            string directory = homeFull;
            for (int index = homeCount; index < names.Count; index++)
            {
                directory = Path.Combine(directory, names[index]);
                string? select = index + 1 < names.Count ? names[index + 1] : null;
                parts.Add(new BreadcrumbPart(names[index], directory, select));
            }

            return parts;
        }

        if (names.Count == 0)
        {
            return [new BreadcrumbPart("/", root, null)];
        }

        var absolute = new List<BreadcrumbPart> { new("/", root, names[0]) };
        string walked = root;
        for (int index = 0; index < names.Count; index++)
        {
            walked = Path.Combine(walked, names[index]);
            string? select = index + 1 < names.Count ? names[index + 1] : null;
            absolute.Add(new BreadcrumbPart(names[index], walked, select));
        }

        return absolute;
    }

    private static bool IsInside(string full, string home, out int homeCount)
    {
        homeCount = 0;
        if (home.Length == 0)
        {
            return false;
        }

        if (string.Equals(full, home, StringComparison.Ordinal))
        {
            homeCount = ComponentCount(home);
            return true;
        }

        string prefix = home.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        homeCount = ComponentCount(home);
        return true;
    }

    private static int ComponentCount(string path)
    {
        int count = 0;
        string cursor = Path.GetFullPath(path);
        while (true)
        {
            string? parent = Path.GetDirectoryName(cursor);
            if (string.IsNullOrEmpty(parent))
            {
                return count;
            }

            count++;
            cursor = parent;
        }
    }
}
