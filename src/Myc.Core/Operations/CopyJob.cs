namespace Myc.Core.Operations;

/// <summary>
/// Copies files, directories, and symlinks. Directories are recursive. Symlinks are copied as
/// links and are not followed. A folder is never copied into itself.
/// </summary>
public sealed class CopyJob : IFileJob
{
    private const int BufferSize = 80 * 1024;

    private readonly IReadOnlyList<string> _sources;
    private readonly string _destination;
    private readonly bool _duplicateInPlace;

    public CopyJob(IReadOnlyList<string> sources, string destination, bool duplicateInPlace = false)
    {
        _sources = sources;
        _destination = destination;
        _duplicateInPlace = duplicateInPlace;
    }

    public async Task<JobResult> RunAsync(
        IProgress<JobProgress>? progress,
        IJobCallbacks callbacks,
        CancellationToken cancellationToken = default)
    {
        int copied = 0;
        int skipped = 0;
        var copiedPaths = new List<string>();
        try
        {
            string destination = Full(_destination);
            if (_sources.Count == 0)
            {
                return JobResult.Done(0, 0);
            }

            if (!CanHoldCopies(destination))
            {
                return JobResult.Fail("A file is in the way.");
            }

            foreach (string source in _sources)
            {
                string full = Full(source);
                if (!Exists(full))
                {
                    return JobResult.Fail("That item is no longer there.");
                }

                if (IsRealDirectory(full) && IsSameOrInside(destination, full))
                {
                    return JobResult.Refuse("Can't copy a folder into itself.");
                }
            }

            if (!_duplicateInPlace && _sources.Any(source => SameFolder(Full(source), destination)))
            {
                string[] names = _sources.Select(source => CopyNames.Duplicate(Path.GetFileName(Full(source)))).ToArray();
                return JobResult.Refuse("The destination is this folder.", names);
            }

            Report(progress, "", 0, 0, 0, 0, JobPhase.Scanning);
            List<PlannedItem> items = [];
            foreach (string source in _sources)
            {
                string full = Full(source);
                string top = Path.GetFileName(full);
                if (_duplicateInPlace && SameFolder(full, destination))
                {
                    top = CopyNames.Duplicate(top);
                }

                if (!await Walk(full, top, items, callbacks, cancellationToken))
                {
                    return new JobResult(JobStatus.Cancelled, null, copied, skipped, [], copiedPaths);
                }
            }

            long bytesTotal = items.Sum(item => item.Size);
            Report(progress, "", 0, items.Count, 0, bytesTotal, JobPhase.Scanning);

            if (!Exists(destination))
            {
                Directory.CreateDirectory(destination);
            }

            long bytesDone = 0;
            ConflictChoice? always = null;
            var skippedPrefixes = new List<string>();
            foreach (PlannedItem item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (UnderSkipped(skippedPrefixes, item.Relative))
                {
                    skipped++;
                    bytesDone += item.Size;
                    Report(progress, item.Relative, copied + skipped, items.Count, bytesDone, bytesTotal, JobPhase.Copying);
                    continue;
                }

                string target = Path.Combine(destination, item.Relative);
                PlaceResult place = await Place(
                    item,
                    target,
                    () => always,
                    choice => always = choice,
                    callbacks,
                    written =>
                    {
                        bytesDone += written;
                        Report(progress, item.Relative, copied + skipped, items.Count, bytesDone, bytesTotal, JobPhase.Copying);
                    },
                    cancellationToken);

                switch (place.Outcome)
                {
                    case PlaceOutcome.Copied:
                        copied++;
                        copiedPaths.Add(item.Source);
                        break;
                    case PlaceOutcome.Skipped:
                        skipped++;
                        bytesDone += item.Size - place.BytesAlreadyCounted;
                        if (item.Kind == PlannedKind.Directory)
                        {
                            skippedPrefixes.Add(item.Relative);
                        }

                        break;
                    case PlaceOutcome.Cancelled:
                        return new JobResult(JobStatus.Cancelled, null, copied, skipped, [], copiedPaths);
                }

                Report(progress, item.Relative, copied + skipped, items.Count, bytesDone, bytesTotal, JobPhase.Copying);
            }

            return JobResult.Done(copied, skipped, copiedPaths);
        }
        catch (OperationCanceledException)
        {
            return new JobResult(JobStatus.Cancelled, null, copied, skipped, [], copiedPaths);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new JobResult(JobStatus.Failed, exception.Message, copied, skipped, [], copiedPaths);
        }
    }

    private static async Task<bool> Walk(
        string source,
        string relative,
        List<PlannedItem> items,
        IJobCallbacks callbacks,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        while (true)
        {
            try
            {
                if (IsSymlink(source))
                {
                    items.Add(new PlannedItem(source, relative, PlannedKind.Symlink, 0, LinkTarget(source)));
                    return true;
                }

                if (IsRealDirectory(source))
                {
                    string[] children = Directory.GetFileSystemEntries(source);
                    items.Add(new PlannedItem(source, relative, PlannedKind.Directory, 0, null));
                    foreach (string child in children)
                    {
                        if (!await Walk(child, Path.Combine(relative, Path.GetFileName(child)), items, callbacks, cancellationToken))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                items.Add(new PlannedItem(source, relative, PlannedKind.File, new FileInfo(source).Length, null));
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                ErrorChoice choice = await callbacks.AskError(new ErrorQuestion(relative, exception.Message), cancellationToken);
                switch (choice)
                {
                    case ErrorChoice.Retry:
                        continue;
                    case ErrorChoice.Skip:
                        return true;
                    default:
                        return false;
                }
            }
        }
    }

    private static async Task<PlaceResult> Place(
        PlannedItem item,
        string target,
        Func<ConflictChoice?> currentAlways,
        Action<ConflictChoice> remember,
        IJobCallbacks callbacks,
        Action<long> onBytes,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            long reported = 0;
            Action<long> track = amount =>
            {
                reported += amount;
                onBytes(amount);
            };

            try
            {
                if (item.Kind == PlannedKind.Directory)
                {
                    if (Exists(target) && !IsRealDirectory(target) && !IsDirectoryLink(target))
                    {
                        ErrorChoice blocked = await callbacks.AskError(
                            new ErrorQuestion(item.Relative, "A file is in the way."),
                            cancellationToken);
                        if (blocked == ErrorChoice.Retry)
                        {
                            continue;
                        }

                        return blocked == ErrorChoice.Skip ? PlaceResult.Skip(0) : PlaceResult.Cancel();
                    }

                    Directory.CreateDirectory(target);
                    return PlaceResult.Copy();
                }

                if (Exists(target))
                {
                    if (IsRealDirectory(target) || IsDirectoryLink(target))
                    {
                        ErrorChoice blocked = await callbacks.AskError(
                            new ErrorQuestion(item.Relative, "A folder is in the way."),
                            cancellationToken);
                        if (blocked == ErrorChoice.Retry)
                        {
                            continue;
                        }

                        return blocked == ErrorChoice.Skip ? PlaceResult.Skip(0) : PlaceResult.Cancel();
                    }

                    ConflictChoice choice = currentAlways() ?? await callbacks.AskConflict(
                        new ConflictQuestion(Path.GetFileName(target), target),
                        cancellationToken);
                    if (choice is ConflictChoice.OverwriteAll or ConflictChoice.SkipAll)
                    {
                        remember(choice);
                    }

                    switch (choice)
                    {
                        case ConflictChoice.Overwrite:
                        case ConflictChoice.OverwriteAll:
                            break;
                        case ConflictChoice.Skip:
                        case ConflictChoice.SkipAll:
                            return PlaceResult.Skip(0);
                        case ConflictChoice.Rename:
                            string parent = Path.GetDirectoryName(target)!;
                            string renamed = CopyNames.NextNumbered(
                                Path.GetFileName(target),
                                candidate => Exists(Path.Combine(parent, candidate)));
                            target = Path.Combine(parent, renamed);
                            break;
                        default:
                            return PlaceResult.Cancel();
                    }
                }

                if (item.Kind == PlannedKind.Symlink)
                {
                    if (Exists(target))
                    {
                        File.Delete(target);
                    }

                    File.CreateSymbolicLink(target, item.LinkTarget ?? "");
                    return PlaceResult.Copy();
                }

                await CopyFile(item.Source, target, track, cancellationToken);
                return PlaceResult.Copy(reported);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (reported != 0)
                {
                    onBytes(-reported);
                }

                ErrorChoice choice = await callbacks.AskError(new ErrorQuestion(item.Relative, exception.Message), cancellationToken);
                switch (choice)
                {
                    case ErrorChoice.Retry:
                        continue;
                    case ErrorChoice.Skip:
                        return PlaceResult.Skip(0);
                    default:
                        return PlaceResult.Cancel();
                }
            }
        }
    }

    private static async Task<long> CopyFile(string source, string target, Action<long> onBytes, CancellationToken cancellationToken)
    {
        string? parent = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        string temp = Path.Combine(parent ?? Path.GetTempPath(), ".myc-copy-" + Guid.NewGuid().ToString("N"));
        long written = 0;
        try
        {
            await using (FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true))
            await using (FileStream output = new(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            {
                byte[] buffer = new byte[BufferSize];
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    written += read;
                    onBytes(read);
                }
            }

            if (IsSymlink(target))
            {
                File.Delete(target);
            }

            File.Move(temp, target, overwrite: true);
            return written;
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private static bool UnderSkipped(List<string> prefixes, string relative)
    {
        foreach (string prefix in prefixes)
        {
            if (relative == prefix || relative.StartsWith(prefix + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanHoldCopies(string destination)
    {
        if (!Exists(destination))
        {
            return true;
        }

        return IsRealDirectory(destination) || IsDirectoryLink(destination);
    }

    private static bool SameFolder(string source, string destination)
    {
        string? parent = Path.GetDirectoryName(source);
        return parent is not null && Full(parent) == destination;
    }

    private static bool IsSameOrInside(string path, string directory) =>
        path.Equals(directory, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Full(string path)
    {
        string full = Path.GetFullPath(path);
        if (full.Length > 1)
        {
            full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return full;
    }

    private static bool Exists(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsSymlink(string path)
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

    private static bool IsRealDirectory(string path) =>
        Directory.Exists(path) && !IsSymlink(path);

    private static bool IsDirectoryLink(string path) =>
        IsSymlink(path) && Directory.Exists(path);

    private static string? LinkTarget(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget ?? new DirectoryInfo(path).LinkTarget;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path) || IsSymlink(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void Report(
        IProgress<JobProgress>? progress,
        string current,
        int done,
        int total,
        long bytesDone,
        long bytesTotal,
        JobPhase phase) =>
        progress?.Report(new JobProgress(current, done, total, bytesDone, bytesTotal, phase));

    private sealed record PlannedItem(string Source, string Relative, PlannedKind Kind, long Size, string? LinkTarget);

    private enum PlannedKind
    {
        File,
        Directory,
        Symlink,
    }

    private enum PlaceOutcome
    {
        Copied,
        Skipped,
        Cancelled,
    }

    private readonly record struct PlaceResult(PlaceOutcome Outcome, long BytesAlreadyCounted)
    {
        public static PlaceResult Copy(long bytesAlreadyCounted = 0) => new(PlaceOutcome.Copied, bytesAlreadyCounted);

        public static PlaceResult Skip(long bytesAlreadyCounted) => new(PlaceOutcome.Skipped, bytesAlreadyCounted);

        public static PlaceResult Cancel() => new(PlaceOutcome.Cancelled, 0);
    }
}
