using System.Globalization;
using System.Text;

namespace Myc.Core.Display;

/// <summary>How a panel row is written. Column counting is supplied by the caller.</summary>
public static class EntryText
{
    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        if (unit == 0)
        {
            return $"{bytes} B";
        }

        bool whole = Math.Abs(value - Math.Round(value)) < 0.05;
        string number = value >= 10 || whole ? value.ToString("0", CultureInfo.InvariantCulture) : value.ToString("0.0", CultureInfo.InvariantCulture);
        return $"{number} {units[unit]}";
    }

    public static string FormatListDate(DateTimeOffset modified) =>
        modified.ToString("dd MMM", CultureInfo.InvariantCulture);

    public static string FormatDetailDate(DateTimeOffset modified) =>
        modified.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Keeps the start of the name and the extension, with an ellipsis in between,
    /// so <c>very-long-filename.txt</c> stays recognisable in a narrow column.
    /// </summary>
    public static string EllipsizeMiddle(string text, int maxColumns, Func<string, int> columns)
    {
        if (maxColumns <= 0)
        {
            return "";
        }

        if (columns(text) <= maxColumns)
        {
            return text;
        }

        const string dots = "…";
        int dotsWidth = Math.Max(1, columns(dots));
        if (maxColumns <= dotsWidth)
        {
            return dots;
        }

        string extension = "";
        string stem = text;
        int dot = text.LastIndexOf('.');
        if (dot > 0 && dot < text.Length - 1)
        {
            string candidate = text[dot..];
            if (columns(candidate) <= maxColumns / 2)
            {
                extension = candidate;
                stem = text[..dot];
            }
        }

        int keep = maxColumns - dotsWidth - columns(extension);
        if (keep <= 0)
        {
            return Take(text, maxColumns - dotsWidth, fromEnd: false, columns) + dots;
        }

        int suffix = Math.Min(4, keep / 3);
        int prefix = keep - suffix;
        if (prefix < 1)
        {
            prefix = keep;
            suffix = 0;
        }

        string head = Take(stem, prefix, fromEnd: false, columns);
        string tail = suffix == 0 ? "" : Take(stem, suffix, fromEnd: true, columns);
        string result = head + dots + tail + extension;
        return columns(result) <= maxColumns ? result : Take(text, maxColumns - dotsWidth, fromEnd: false, columns) + dots;
    }

    private static string Take(string text, int maxColumns, bool fromEnd, Func<string, int> columns)
    {
        if (maxColumns <= 0)
        {
            return "";
        }

        var runes = text.EnumerateRunes().ToArray();
        var builder = new StringBuilder();
        if (!fromEnd)
        {
            foreach (Rune rune in runes)
            {
                if (columns(builder.ToString() + rune) > maxColumns)
                {
                    break;
                }

                builder.Append(rune);
            }

            return builder.ToString();
        }

        for (int index = runes.Length - 1; index >= 0; index--)
        {
            string next = runes[index].ToString() + builder;
            if (columns(next) > maxColumns)
            {
                break;
            }

            builder.Insert(0, runes[index].ToString());
        }

        return builder.ToString();
    }
}
