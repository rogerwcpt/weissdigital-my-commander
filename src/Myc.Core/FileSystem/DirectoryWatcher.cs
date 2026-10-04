namespace Myc.Core.FileSystem;

/// <summary>
/// Watches one directory for changes made outside myc. A burst of events becomes a single
/// callback about 250 ms after the last one. The callback runs on a background thread.
/// </summary>
public sealed class DirectoryWatcher : IDisposable
{
    private readonly Action _onChanged;
    private readonly TimeSpan _delay;
    private readonly object _gate = new();
    private readonly Timer _timer;
    private FileSystemWatcher? _watcher;
    private string? _path;
    private int _disposed;

    public DirectoryWatcher(Action onChanged, TimeSpan? delay = null)
    {
        _onChanged = onChanged;
        _delay = delay ?? TimeSpan.FromMilliseconds(250);
        _timer = new Timer(_ => Fire(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Follows <paramref name="directory"/>. A missing path, or the path already being watched, is ignored.</summary>
    public void Watch(string? directory)
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        string? full = Normalize(directory);
        lock (_gate)
        {
            if (Volatile.Read(ref _disposed) == 1 || string.Equals(full, _path, StringComparison.Ordinal))
            {
                return;
            }

            _path = full;
            StopWatcher();
            if (full is null || !Directory.Exists(full))
            {
                return;
            }

            var watcher = new FileSystemWatcher(full)
            {
                Filter = "*",
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size
                    | NotifyFilters.Attributes,
                InternalBufferSize = 16 * 4096,
            };
            watcher.Created += (_, _) => Schedule();
            watcher.Deleted += (_, _) => Schedule();
            watcher.Changed += (_, _) => Schedule();
            watcher.Renamed += (_, _) => Schedule();
            watcher.Error += (_, _) => Schedule();
            try
            {
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException)
            {
                watcher.Dispose();
                return;
            }

            _watcher = watcher;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        lock (_gate)
        {
            _timer.Dispose();
            StopWatcher();
            _path = null;
        }
    }

    private void Schedule()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        try
        {
            _timer.Change(_delay, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void Fire()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        try
        {
            _onChanged();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void StopWatcher()
    {
        if (_watcher is null)
        {
            return;
        }

        try
        {
            _watcher.EnableRaisingEvents = false;
        }
        catch (Exception exception) when (exception is ObjectDisposedException or InvalidOperationException or IOException)
        {
        }

        _watcher.Dispose();
        _watcher = null;
    }

    private static string? Normalize(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(directory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
