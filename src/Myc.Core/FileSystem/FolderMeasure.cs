using Myc.Core.Operations;

namespace Myc.Core.FileSystem;

/// <summary>One child of a measured folder. A symlink is not followed.</summary>
public readonly record struct FolderChild(string Name, long Bytes, bool Directory, bool OtherVolume);

/// <summary>Sizes of the immediate children of one folder. Directory bytes are recursive.</summary>
public readonly record struct FolderMeasureResult(long Total, IReadOnlyList<FolderChild> Children, int Skipped);

/// <summary>
/// Measures one folder, one level of blocks. Another volume is a single child and is not entered.
/// </summary>
public static class FolderMeasure
{
    public static FolderMeasureResult Scan(string directory, CancellationToken cancellation, Action<int>? progress = null)
    {
        var children = new List<FolderChild>();
        int skipped = 0;
        int seen = 0;
        string mount = Volumes.Mount(directory);
        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(directory).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new FolderMeasureResult(0, [], 1);
        }

        foreach (string path in entries)
        {
            cancellation.ThrowIfCancellationRequested();
            string name = Path.GetFileName(path);
            if (name is "." or "..")
            {
                continue;
            }

            try
            {
                if (IsLink(path))
                {
                    children.Add(new FolderChild(name, 0, false, false));
                    continue;
                }

                if (Directory.Exists(path))
                {
                    if (!Volumes.Same(directory, path))
                    {
                        children.Add(new FolderChild(name, 0, true, true));
                        continue;
                    }

                    long bytes = Walk(path, mount, ref seen, ref skipped, progress, cancellation);
                    children.Add(new FolderChild(name, bytes, true, false));
                    continue;
                }

                long length = new FileInfo(path).Length;
                seen++;
                Report(seen, progress);
                children.Add(new FolderChild(name, length, false, false));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                skipped++;
                children.Add(new FolderChild(name, 0, false, false));
            }
        }

        children.Sort(static (left, right) => right.Bytes.CompareTo(left.Bytes));
        long total = 0;
        foreach (FolderChild child in children)
        {
            total += child.Bytes;
        }

        return new FolderMeasureResult(total, children, skipped);
    }

    private static long Walk(
        string directory,
        string mount,
        ref int seen,
        ref int skipped,
        Action<int>? progress,
        CancellationToken cancellation)
    {
        string[] entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(directory).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            skipped++;
            return 0;
        }

        long total = 0;
        foreach (string path in entries)
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                if (IsLink(path))
                {
                    continue;
                }

                if (Directory.Exists(path))
                {
                    if (Volumes.Mount(path) != mount)
                    {
                        continue;
                    }

                    total += Walk(path, mount, ref seen, ref skipped, progress, cancellation);
                    continue;
                }

                total += new FileInfo(path).Length;
                seen++;
                Report(seen, progress);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                skipped++;
            }
        }

        return total;
    }

    private static bool IsLink(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Report(int seen, Action<int>? progress) => progress?.Invoke(seen);
}
