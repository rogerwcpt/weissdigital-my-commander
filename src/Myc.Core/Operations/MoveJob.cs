namespace Myc.Core.Operations;

/// <summary>
/// Moves the selection. On the same mount each item is renamed. Across mounts, or when the
/// name is already taken, it is copied and the source is removed only after that copy succeeds.
/// A same-folder duplicate keeps the original.
/// </summary>
public sealed class MoveJob : IFileJob
{
    private readonly IReadOnlyList<string> _sources;
    private readonly string _destination;
    private readonly bool _duplicateInPlace;
    private readonly bool _copyThenDelete;

    public MoveJob(
        IReadOnlyList<string> sources,
        string destination,
        bool duplicateInPlace = false,
        bool copyThenDelete = false)
    {
        _sources = sources;
        _destination = destination;
        _duplicateInPlace = duplicateInPlace;
        _copyThenDelete = copyThenDelete;
    }

    public async Task<JobResult> RunAsync(
        IProgress<JobProgress>? progress,
        IJobCallbacks callbacks,
        CancellationToken cancellationToken = default)
    {
        if (_sources.Count == 0)
        {
            return JobResult.Done(0, 0);
        }

        string destination = Path.GetFullPath(_destination);
        if (_sources.Any(source => IsRealDirectory(Path.GetFullPath(source)) && IsSameOrInside(destination, Path.GetFullPath(source))))
        {
            return JobResult.Refuse("Can't copy a folder into itself.");
        }

        if (!_duplicateInPlace && _sources.Any(source => SameFolder(Path.GetFullPath(source), destination)))
        {
            string[] names = _sources.Select(source => CopyNames.Duplicate(Path.GetFileName(Path.GetFullPath(source)))).ToArray();
            return JobResult.Refuse("The destination is this folder.", names);
        }

        if (_duplicateInPlace || _copyThenDelete || !CanRename(destination))
        {
            return await CopyThenRemove(destination, progress, callbacks, cancellationToken);
        }

        int moved = 0;
        try
        {
            if (!Directory.Exists(destination))
            {
                Directory.CreateDirectory(destination);
            }

            Report(progress, "", 0, _sources.Count, JobPhase.Scanning);
            foreach (string source in _sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string full = Path.GetFullPath(source);
                string target = Path.Combine(destination, Path.GetFileName(full));
                Report(progress, Path.GetFileName(full), moved, _sources.Count, JobPhase.Copying);
                try
                {
                    MoveEntry(full, target);
                }
                catch (IOException exception) when (IsCrossDevice(exception))
                {
                    JobResult copied = await CopyThenRemove([full], destination, progress, callbacks, cancellationToken);
                    if (copied.Status != JobStatus.Completed)
                    {
                        return copied;
                    }
                }

                moved++;
                Report(progress, Path.GetFileName(full), moved, _sources.Count, JobPhase.Copying);
            }

            return JobResult.Done(moved, 0);
        }
        catch (OperationCanceledException)
        {
            return new JobResult(JobStatus.Cancelled, null, moved, 0, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new JobResult(JobStatus.Failed, exception.Message, moved, 0, []);
        }
    }

    private bool CanRename(string destination)
    {
        if (_sources.Count == 0 || !Volumes.Same(_sources[0], destination))
        {
            return false;
        }

        foreach (string source in _sources)
        {
            string target = Path.Combine(destination, Path.GetFileName(Path.GetFullPath(source)));
            if (Exists(target))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<JobResult> CopyThenRemove(
        string destination,
        IProgress<JobProgress>? progress,
        IJobCallbacks callbacks,
        CancellationToken cancellationToken) =>
        await CopyThenRemove(_sources, destination, progress, callbacks, cancellationToken);

    private async Task<JobResult> CopyThenRemove(
        IReadOnlyList<string> sources,
        string destination,
        IProgress<JobProgress>? progress,
        IJobCallbacks callbacks,
        CancellationToken cancellationToken)
    {
        JobResult copied = await new CopyJob(sources, destination, _duplicateInPlace)
            .RunAsync(progress, callbacks, cancellationToken);
        if (copied.Status != JobStatus.Completed || _duplicateInPlace)
        {
            return copied;
        }

        try
        {
            DeleteCopied(copied.PathsCopied);
            return copied;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new JobResult(JobStatus.Failed, exception.Message, copied.Copied, copied.Skipped, [], copied.PathsCopied);
        }
    }

    private static void DeleteCopied(IReadOnlyList<string> paths)
    {
        foreach (string path in paths.OrderByDescending(path => path.Length))
        {
            if (IsSymlink(path))
            {
                File.Delete(path);
                continue;
            }

            if (File.Exists(path))
            {
                File.Delete(path);
                continue;
            }

            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
    }

    private static void MoveEntry(string source, string destination)
    {
        if (IsSymlink(source) || !Directory.Exists(source))
        {
            File.Move(source, destination);
            return;
        }

        Directory.Move(source, destination);
    }

    private static bool IsCrossDevice(IOException exception) =>
        (exception.HResult & 0xFFFF) == 18
        || exception.Message.Contains("cross-device", StringComparison.OrdinalIgnoreCase);

    private static bool SameFolder(string source, string destination)
    {
        string? parent = Path.GetDirectoryName(source);
        return parent is not null && string.Equals(Path.GetFullPath(parent), destination, StringComparison.Ordinal);
    }

    private static bool IsSameOrInside(string path, string directory) =>
        path.Equals(directory, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool IsRealDirectory(string path) =>
        Directory.Exists(path) && !IsSymlink(path);

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

    private static bool Exists(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Report(IProgress<JobProgress>? progress, string current, int done, int total, JobPhase phase) =>
        progress?.Report(new JobProgress(current, done, total, 0, 0, phase));
}
