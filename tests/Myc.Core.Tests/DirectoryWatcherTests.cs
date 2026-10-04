using Myc.Core.FileSystem;

namespace Myc.Core.Tests;

public class DirectoryWatcherTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "myc-watch-" + Guid.NewGuid().ToString("N"));

    public DirectoryWatcherTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void A_burst_of_changes_refreshes_once()
    {
        int refreshes = 0;
        using var watcher = new DirectoryWatcher(() => Interlocked.Increment(ref refreshes), TimeSpan.FromMilliseconds(200));
        watcher.Watch(_root);
        Thread.Sleep(200);

        for (int i = 0; i < 12; i++)
        {
            File.WriteAllText(Path.Combine(_root, $"note-{i}.txt"), "x");
        }

        Thread.Sleep(120);
        Assert.Equal(0, Volatile.Read(ref refreshes));

        var seen = SpinWait.SpinUntil(() => Volatile.Read(ref refreshes) >= 1, TimeSpan.FromSeconds(3));
        Assert.True(seen, "The directory never reported a change.");
        Thread.Sleep(400);
        Assert.InRange(Volatile.Read(ref refreshes), 1, 3);
    }

    [Fact]
    public void A_missing_directory_is_not_watched()
    {
        using var watcher = new DirectoryWatcher(() => { });

        watcher.Watch(Path.Combine(_root, "absent"));
        watcher.Watch(null);
        watcher.Dispose();
        watcher.Dispose();
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
