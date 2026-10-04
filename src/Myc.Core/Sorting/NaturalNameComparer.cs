namespace Myc.Core.Sorting;

/// <summary>
/// Case-insensitive order where digit runs compare as numbers, so <c>file2</c> comes before
/// <c>file10</c>. Equal numbers with different padding sort the shorter text first
/// (<c>file2</c> before <c>file02</c>).
/// </summary>
public sealed class NaturalNameComparer : IComparer<string>
{
    public static NaturalNameComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        x = x.Normalize(System.Text.NormalizationForm.FormC);
        y = y.Normalize(System.Text.NormalizationForm.FormC);

        int ix = 0;
        int iy = 0;
        while (ix < x.Length && iy < y.Length)
        {
            if (char.IsAsciiDigit(x[ix]) && char.IsAsciiDigit(y[iy]))
            {
                int xStart = ix;
                int yStart = iy;
                while (ix < x.Length && char.IsAsciiDigit(x[ix]))
                {
                    ix++;
                }

                while (iy < y.Length && char.IsAsciiDigit(y[iy]))
                {
                    iy++;
                }

                int compared = CompareNumeric(x.AsSpan(xStart, ix - xStart), y.AsSpan(yStart, iy - yStart));
                if (compared != 0)
                {
                    return compared;
                }
            }
            else
            {
                int xEnd = ix;
                int yEnd = iy;
                while (xEnd < x.Length && !char.IsAsciiDigit(x[xEnd]))
                {
                    xEnd++;
                }

                while (yEnd < y.Length && !char.IsAsciiDigit(y[yEnd]))
                {
                    yEnd++;
                }

                ReadOnlySpan<char> xChunk = x.AsSpan(ix, xEnd - ix);
                ReadOnlySpan<char> yChunk = y.AsSpan(iy, yEnd - iy);
                int compared = xChunk.CompareTo(yChunk, StringComparison.OrdinalIgnoreCase);
                if (compared == 0)
                {
                    compared = xChunk.CompareTo(yChunk, StringComparison.Ordinal);
                }

                if (compared != 0)
                {
                    return compared;
                }

                ix = xEnd;
                iy = yEnd;
            }
        }

        return (x.Length - ix).CompareTo(y.Length - iy);
    }

    private static int CompareNumeric(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
    {
        int xTrim = TrimZeros(x);
        int yTrim = TrimZeros(y);
        int compared = (x.Length - xTrim).CompareTo(y.Length - yTrim);
        if (compared != 0)
        {
            return compared;
        }

        compared = x[xTrim..].CompareTo(y[yTrim..], StringComparison.Ordinal);
        return compared != 0 ? compared : x.Length.CompareTo(y.Length);
    }

    private static int TrimZeros(ReadOnlySpan<char> value)
    {
        int index = 0;
        while (index < value.Length - 1 && value[index] == '0')
        {
            index++;
        }

        return index;
    }
}
