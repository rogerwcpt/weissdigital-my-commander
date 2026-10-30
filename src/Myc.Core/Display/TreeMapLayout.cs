namespace Myc.Core.Display;

/// <summary>One rectangle in a size map. Coordinates are cells, origin at the top left.</summary>
public readonly record struct MapBlock(int X, int Y, int Width, int Height);

/// <summary>
/// Squarified treemap. The blocks tile the rectangle: no gaps, no overlaps.
/// A larger weight receives more cells.
/// </summary>
public static class TreeMapLayout
{
    public static IReadOnlyList<MapBlock> Arrange(int width, int height, IReadOnlyList<long> sizes)
    {
        if (width <= 0 || height <= 0 || sizes.Count == 0)
        {
            return [];
        }

        long[] weights = new long[sizes.Count];
        long total = 0;
        for (int i = 0; i < sizes.Count; i++)
        {
            weights[i] = Math.Max(0, sizes[i]);
            total += weights[i];
        }

        if (total <= 0)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = 1;
            }

            total = weights.Length;
        }

        var blocks = new MapBlock[sizes.Count];
        Place(weights, 0, weights.Length, total, 0, 0, width, height, blocks);
        return blocks;
    }

    private static void Place(
        long[] weights,
        int start,
        int count,
        long total,
        int x,
        int y,
        int width,
        int height,
        MapBlock[] blocks)
    {
        if (count <= 0 || width <= 0 || height <= 0 || total <= 0)
        {
            return;
        }

        if (count == 1 || width == 1 || height == 1)
        {
            Slice(weights, start, count, x, y, width, height, blocks);
            return;
        }

        bool wide = width >= height;
        int side = wide ? height : width;
        int span = wide ? width : height;
        int row = 1;
        long rowSum = weights[start];
        double best = Worst(weights, start, 1, rowSum, side);
        for (int n = 2; n <= count; n++)
        {
            long nextSum = rowSum + weights[start + n - 1];
            double score = Worst(weights, start, n, nextSum, side);
            if (score > best)
            {
                break;
            }

            best = score;
            row = n;
            rowSum = nextSum;
        }

        int thickness = row == count
            ? span
            : Math.Clamp((int)Math.Round(span * (rowSum / (double)total)), 1, span - 1);
        if (wide)
        {
            Slice(weights, start, row, x, y, thickness, height, blocks);
            Place(weights, start + row, count - row, total - rowSum, x + thickness, y, width - thickness, height, blocks);
        }
        else
        {
            Slice(weights, start, row, x, y, width, thickness, blocks);
            Place(weights, start + row, count - row, total - rowSum, x, y + thickness, width, height - thickness, blocks);
        }
    }

    private static void Slice(long[] weights, int start, int count, int x, int y, int width, int height, MapBlock[] blocks)
    {
        bool horizontal = width >= height;
        int span = horizontal ? width : height;
        long sum = 0;
        for (int i = 0; i < count; i++)
        {
            sum += Math.Max(1, weights[start + i]);
        }

        int cursor = 0;
        for (int i = 0; i < count; i++)
        {
            int take;
            if (i == count - 1)
            {
                take = Math.Max(0, span - cursor);
            }
            else
            {
                int room = span - cursor;
                int rest = count - i - 1;
                int rounded = (int)Math.Round(span * (Math.Max(1, weights[start + i]) / (double)sum));
                int max = room - rest;
                take = max <= 0 ? 0 : Math.Clamp(rounded, 1, max);
            }

            blocks[start + i] = horizontal
                ? new MapBlock(x + cursor, y, take, height)
                : new MapBlock(x, y + cursor, width, take);
            cursor += take;
        }
    }

    /// <summary>Higher is a worse row. The side is the length the row is stacked along.</summary>
    private static double Worst(long[] weights, int start, int count, long sum, int side)
    {
        if (sum <= 0 || side <= 0)
        {
            return double.MaxValue;
        }

        long max = 0;
        long min = long.MaxValue;
        for (int i = 0; i < count; i++)
        {
            long value = Math.Max(1, weights[start + i]);
            if (value > max)
            {
                max = value;
            }

            if (value < min)
            {
                min = value;
            }
        }

        double total = sum;
        double length = side;
        return Math.Max(length * length * max / (total * total), total * total / (length * length * min));
    }
}
