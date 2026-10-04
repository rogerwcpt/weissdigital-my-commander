using Myc.Core.Platform;

namespace Myc.Core.Operations;

/// <summary>
/// Moves each selected item to Trash, or permanently deletes a tree. Symlinks are removed as
/// links and are never followed. Folders are one confirmation in the UI; this job does not ask again per folder.
/// </summary>
public sealed class DeleteJob : IFileJob
{
    private readonly IReadOnlyList<string> _sources;
    private readonly bool _permanent;
    private readonly ITrash? _trash;

    public DeleteJob(IReadOnlyList<string> sources, bool permanent, ITrash? trash = null)
    {
        _sources = sources;
        _permanent = permanent;
        _trash = trash;
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

        if (!_permanent && _trash is null)
        {
            return JobResult.Fail("Trash is only set up on macOS.");
        }

        int removed = 0;
        int skipped = 0;
        try
        {
            Report(progress, "", 0, 0, JobPhase.Scanning);
            List<string> plan = [];
            foreach (string source in _sources)
            {
                string full = Path.GetFullPath(source);
                if (!Exists(full))
                {
                    return JobResult.Fail("That item is no longer there.");
                }

                if (_permanent)
                {
                    if (!await Walk(full, plan, callbacks, cancellationToken))
                    {
                        return new JobResult(JobStatus.Cancelled, null, removed, skipped, []);
                    }
                }
                else
                {
                    plan.Add(full);
                }
            }

            if (_permanent)
            {
                plan.Sort((left, right) => right.Length.CompareTo(left.Length));
            }

            Report(progress, "", 0, plan.Count, JobPhase.Scanning);
            foreach (string path in plan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = Path.GetFileName(path);
                while (true)
                {
                    try
                    {
                        if (_permanent)
                        {
                            DeleteOne(path);
                        }
                        else
                        {
                            TrashMove move = _trash!.MoveToTrash(path);
                            if (move.Error is not null)
                            {
                                throw new IOException(move.Error);
                            }
                        }

                        removed++;
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        ErrorChoice choice = await callbacks.AskError(new ErrorQuestion(name, exception.Message), cancellationToken);
                        switch (choice)
                        {
                            case ErrorChoice.Retry:
                                continue;
                            case ErrorChoice.Skip:
                                skipped++;
                                break;
                            default:
                                return new JobResult(JobStatus.Cancelled, null, removed, skipped, []);
                        }

                        break;
                    }
                }

                Report(progress, name, removed + skipped, plan.Count, JobPhase.Copying);
            }

            return JobResult.Done(removed, skipped);
        }
        catch (OperationCanceledException)
        {
            return new JobResult(JobStatus.Cancelled, null, removed, skipped, []);
        }
    }

    private static async Task<bool> Walk(string path, List<string> plan, IJobCallbacks callbacks, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (IsSymlink(path))
                {
                    plan.Add(path);
                    return true;
                }

                if (Directory.Exists(path))
                {
                    string[] children = Directory.GetFileSystemEntries(path);
                    plan.Add(path);
                    foreach (string child in children)
                    {
                        if (!await Walk(child, plan, callbacks, cancellationToken))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                plan.Add(path);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                ErrorChoice choice = await callbacks.AskError(new ErrorQuestion(Path.GetFileName(path), exception.Message), cancellationToken);
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

    private static void DeleteOne(string path)
    {
        if (!Exists(path))
        {
            return;
        }

        if (IsSymlink(path))
        {
            File.Delete(path);
            return;
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path);
            return;
        }

        File.Delete(path);
    }

    private static void Report(IProgress<JobProgress>? progress, string current, int done, int total, JobPhase phase) =>
        progress?.Report(new JobProgress(current, done, total, 0, 0, phase));

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
}
