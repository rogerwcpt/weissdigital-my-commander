namespace Myc.Core.Sorting;

/// <summary>
/// The four panel orders. Stored in <c>state.json</c> as these names.
/// <c>..</c> stays first, then directories, then everything else, in every order.
/// </summary>
public static class PanelSort
{
    public const string Name = "name";
    public const string Extension = "extension";
    public const string Size = "size";
    public const string Modified = "modified";

    public static string Normalize(string? sort)
    {
        if (sort is not null && sort.Equals(Extension, StringComparison.OrdinalIgnoreCase))
        {
            return Extension;
        }

        if (sort is not null && sort.Equals(Size, StringComparison.OrdinalIgnoreCase))
        {
            return Size;
        }

        if (sort is not null && sort.Equals(Modified, StringComparison.OrdinalIgnoreCase))
        {
            return Modified;
        }

        return Name;
    }

    /// <summary>
    /// Column titles for the current order. The arrow sits on that column, or on the
    /// name when the column has been dropped because the panel is narrow.
    /// </summary>
    public static ColumnHeaders Headers(string sort, bool descending, bool showSize, bool showModified)
    {
        string mark = descending ? " ▼" : " ▲";
        string name = "Name";
        string size = "Size";
        string modified = "Modified";
        switch (Normalize(sort))
        {
            case Extension:
                name = "Extension" + mark;
                break;
            case Size when showSize:
                size += mark;
                break;
            case Size:
                name = "Size" + mark;
                break;
            case Modified when showModified:
                modified += mark;
                break;
            case Modified:
                name = "Modified" + mark;
                break;
            default:
                name += mark;
                break;
        }

        return new ColumnHeaders(name, size, modified);
    }
}

public readonly record struct ColumnHeaders(string Name, string Size, string Modified);
