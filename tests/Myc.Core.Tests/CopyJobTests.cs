using Myc.Core.Operations;

namespace Myc.Core.Tests;

public class CopyJobTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("myc-").FullName;

    [Fact]
    public async Task Copies_a_file_and_reports_the_scan_before_the_bytes()
    {
        string source = Path.Combine(_root, "src");
        string destination = Path.Combine(_root, "dst");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(source, "notes.md"), "hello");

        List<JobProgress> progress = [];
        JobResult result = await new CopyJob([Path.Combine(source, "notes.md")], destination)
            .RunAsync(new ProgressList(progress), new ScriptedCallbacks(), CancellationToken.None);

        Assert.Equal(JobStatus.Completed, result.Status);
        Assert.Equal("hello", File.ReadAllText(Path.Combine(destination, "notes.md")));
        int firstCopy = progress.FindIndex(item => item.Phase == JobPhase.Copying && item.BytesDone > 0);
        int lastScan = progress.FindLastIndex(item => item.Phase == JobPhase.Scanning);
        Assert.True(lastScan >= 0 && lastScan < firstCopy);
        Assert.Equal(5, progress[lastScan].BytesTotal);
        Assert.Equal(5, progress[^1].BytesDone);
    }

    [Fact]
    public async Task Copies_a_tree_and_keeps_symlinks_as_links()
    {
        string source = Path.Combine(_root, "src");
        string destination = Path.Combine(_root, "dst");
        string outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "secret.txt"), "nope");
        File.WriteAllText(Path.Combine(source, "keep.txt"), "yes");
        File.CreateSymbolicLink(Path.Combine(source, "jump"), outside);
        File.CreateSymbolicLink(Path.Combine(source, "broken"), Path.Combine(_root, "missing"));

        List<JobProgress> progress = [];
        JobResult result = await Copy(source, destination, progress);

        Assert.Equal(JobStatus.Completed, result.Status);
        string copied = Path.Combine(destination, "src");
        Assert.Equal("yes", File.ReadAllText(Path.Combine(copied, "keep.txt")));
        Assert.True(File.GetAttributes(Path.Combine(copied, "jump")).HasFlag(FileAttributes.ReparsePoint));
        Assert.Equal(outside, new FileInfo(Path.Combine(copied, "jump")).LinkTarget);
        Assert.Equal(Path.Combine(_root, "missing"), new FileInfo(Path.Combine(copied, "broken")).LinkTarget);
        Assert.Equal(4, progress.Last(item => item.Phase == JobPhase.Scanning).ItemsTotal);
    }

    [Fact]
    public async Task Refuses_to_copy_a_folder_into_itself_or_its_subtree()
    {
        string folder = Path.Combine(_root, "folder");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "notes.md"), "hello");
        string nested = Path.Combine(folder, "nested");
        Directory.CreateDirectory(nested);

        JobResult intoSelf = await new CopyJob([folder], folder).RunAsync(null, new ScriptedCallbacks(), CancellationToken.None);
        JobResult intoChild = await new CopyJob([folder], nested).RunAsync(null, new ScriptedCallbacks(), CancellationToken.None);

        Assert.Equal(JobStatus.Refused, intoSelf.Status);
        Assert.Equal("Can't copy a folder into itself.", intoSelf.Message);
        Assert.Equal(JobStatus.Refused, intoChild.Status);
        Assert.Empty(Directory.GetFileSystemEntries(nested));
        Assert.Equal("hello", File.ReadAllText(Path.Combine(folder, "notes.md")));
    }

    [Fact]
    public async Task Same_folder_is_refused_until_duplicate_is_confirmed()
    {
        File.WriteAllText(Path.Combine(_root, "notes.md"), "hello");

        JobResult refused = await new CopyJob([Path.Combine(_root, "notes.md")], _root).RunAsync(null, new ScriptedCallbacks(), CancellationToken.None);

        Assert.Equal(JobStatus.Refused, refused.Status);
        Assert.Equal(["notes copy.md"], refused.DuplicateNames);
        Assert.False(File.Exists(Path.Combine(_root, "notes copy.md")));

        JobResult copied = await new CopyJob([Path.Combine(_root, "notes.md")], _root, duplicateInPlace: true)
            .RunAsync(null, new ScriptedCallbacks(), CancellationToken.None);

        Assert.Equal(JobStatus.Completed, copied.Status);
        Assert.Equal("hello", File.ReadAllText(Path.Combine(_root, "notes.md")));
        Assert.Equal("hello", File.ReadAllText(Path.Combine(_root, "notes copy.md")));
    }

    [Fact]
    public async Task Conflict_overwrite_skip_and_rename()
    {
        string source = Path.Combine(_root, "src");
        string destination = Path.Combine(_root, "dst");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(source, "notes.md"), "new");
        File.WriteAllText(Path.Combine(destination, "notes.md"), "old");

        ScriptedCallbacks skip = new();
        skip.Conflicts.Enqueue(ConflictChoice.Skip);
        JobResult skipped = await new CopyJob([Path.Combine(source, "notes.md")], destination).RunAsync(null, skip, CancellationToken.None);
        Assert.Equal(1, skipped.Skipped);
        Assert.Equal("old", File.ReadAllText(Path.Combine(destination, "notes.md")));

        ScriptedCallbacks rename = new();
        rename.Conflicts.Enqueue(ConflictChoice.Rename);
        JobResult renamed = await new CopyJob([Path.Combine(source, "notes.md")], destination).RunAsync(null, rename, CancellationToken.None);
        Assert.Equal("old", File.ReadAllText(Path.Combine(destination, "notes.md")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(destination, "notes (2).md")));

        File.WriteAllText(Path.Combine(destination, "notes.md"), "old");
        ScriptedCallbacks overwrite = new();
        overwrite.Conflicts.Enqueue(ConflictChoice.Overwrite);
        await new CopyJob([Path.Combine(source, "notes.md")], destination).RunAsync(null, overwrite, CancellationToken.None);
        Assert.Equal("new", File.ReadAllText(Path.Combine(destination, "notes.md")));
    }

    [Fact]
    public async Task Overwrite_all_asks_once()
    {
        string source = Path.Combine(_root, "src");
        string destination = Path.Combine(_root, "dst");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(source, "a.txt"), "a");
        File.WriteAllText(Path.Combine(source, "b.txt"), "b");
        File.WriteAllText(Path.Combine(destination, "a.txt"), "old");
        File.WriteAllText(Path.Combine(destination, "b.txt"), "old");

        ScriptedCallbacks callbacks = new();
        callbacks.Conflicts.Enqueue(ConflictChoice.OverwriteAll);
        JobResult result = await new CopyJob(
            [Path.Combine(source, "a.txt"), Path.Combine(source, "b.txt")],
            destination).RunAsync(null, callbacks, CancellationToken.None);

        Assert.Equal(JobStatus.Completed, result.Status);
        Assert.Equal(1, callbacks.ConflictAsks);
        Assert.Equal("a", File.ReadAllText(Path.Combine(destination, "a.txt")));
        Assert.Equal("b", File.ReadAllText(Path.Combine(destination, "b.txt")));
    }

    [Fact]
    public async Task Cancel_stops_after_the_current_item_and_keeps_what_finished()
    {
        string source = Path.Combine(_root, "src");
        string destination = Path.Combine(_root, "dst");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(source, "a.txt"), "a");
        File.WriteAllText(Path.Combine(source, "b.txt"), "b");

        using CancellationTokenSource cancel = new();
        var progress = new ProgressList([]);
        progress.OnReport = item =>
        {
            if (item.Phase == JobPhase.Copying && item.ItemsDone >= 1)
            {
                cancel.Cancel();
            }
        };

        JobResult result = await new CopyJob(
            [Path.Combine(source, "a.txt"), Path.Combine(source, "b.txt")],
            destination).RunAsync(progress, new ScriptedCallbacks(), cancel.Token);

        Assert.Equal(JobStatus.Cancelled, result.Status);
        Assert.Equal("a", File.ReadAllText(Path.Combine(destination, "a.txt")));
        Assert.False(File.Exists(Path.Combine(destination, "b.txt")));
    }

    [Fact]
    public async Task A_locked_directory_can_be_skipped_without_losing_the_rest()
    {
        string source = Path.Combine(_root, "src");
        string destination = Path.Combine(_root, "dst");
        string locked = Path.Combine(source, "locked");
        Directory.CreateDirectory(locked);
        File.WriteAllText(Path.Combine(source, "ok.txt"), "yes");
        File.WriteAllText(Path.Combine(locked, "secret.txt"), "no");
        SetMode(locked, UnixFileMode.None);
        try
        {
            ScriptedCallbacks callbacks = new();
            callbacks.Errors.Enqueue(ErrorChoice.Skip);
            JobResult result = await Copy(source, destination, null, callbacks);

            Assert.Equal(JobStatus.Completed, result.Status);
            Assert.Equal("yes", File.ReadAllText(Path.Combine(destination, "src", "ok.txt")));
            Assert.False(Directory.Exists(Path.Combine(destination, "src", "locked")));
        }
        finally
        {
            SetMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public async Task Retry_reads_a_file_after_it_becomes_readable()
    {
        string source = Path.Combine(_root, "secret.txt");
        string destination = Path.Combine(_root, "dst");
        Directory.CreateDirectory(destination);
        File.WriteAllText(source, "later");
        SetMode(source, UnixFileMode.None);
        try
        {
            int asks = 0;
            ScriptedCallbacks callbacks = new();
            callbacks.OnError = () =>
            {
                asks++;
                SetMode(source, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                return ErrorChoice.Retry;
            };

            JobResult result = await new CopyJob([source], destination).RunAsync(null, callbacks, CancellationToken.None);

            Assert.Equal(1, asks);
            Assert.Equal(JobStatus.Completed, result.Status);
            Assert.Equal("later", File.ReadAllText(Path.Combine(destination, "secret.txt")));
        }
        finally
        {
            SetMode(source, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public async Task An_existing_directory_is_merged_without_a_question()
    {
        string source = Path.Combine(_root, "src", "sub");
        string destination = Path.Combine(_root, "dst", "sub");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(source, "a.txt"), "a");
        File.WriteAllText(Path.Combine(destination, "b.txt"), "b");

        ScriptedCallbacks callbacks = new();
        JobResult result = await new CopyJob([Path.Combine(_root, "src", "sub")], Path.Combine(_root, "dst"))
            .RunAsync(null, callbacks, CancellationToken.None);

        Assert.Equal(JobStatus.Completed, result.Status);
        Assert.Equal(0, callbacks.ConflictAsks);
        Assert.Equal("a", File.ReadAllText(Path.Combine(destination, "a.txt")));
        Assert.Equal("b", File.ReadAllText(Path.Combine(destination, "b.txt")));
    }

    public void Dispose()
    {
        try
        {
            foreach (string directory in Directory.EnumerateDirectories(_root, "*", SearchOption.AllDirectories))
            {
                SetMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        Directory.Delete(_root, recursive: true);
    }

    private async Task<JobResult> Copy(string source, string destination, List<JobProgress>? progress, ScriptedCallbacks? callbacks = null)
    {
        Directory.CreateDirectory(destination);
        IProgress<JobProgress>? report = progress is null ? null : new ProgressList(progress);
        return await new CopyJob([source], destination).RunAsync(report, callbacks ?? new ScriptedCallbacks(), CancellationToken.None);
    }

    private static void SetMode(string path, UnixFileMode mode)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, mode);
        }
    }

    private sealed class ProgressList(List<JobProgress> items) : IProgress<JobProgress>
    {
        public Action<JobProgress>? OnReport { get; set; }

        public void Report(JobProgress value)
        {
            items.Add(value);
            OnReport?.Invoke(value);
        }
    }

    private sealed class ScriptedCallbacks : IJobCallbacks
    {
        public Queue<ConflictChoice> Conflicts { get; } = new();

        public Queue<ErrorChoice> Errors { get; } = new();

        public int ConflictAsks { get; private set; }

        public Func<ErrorChoice>? OnError { get; set; }

        public ValueTask<ConflictChoice> AskConflict(ConflictQuestion question, CancellationToken cancellationToken)
        {
            ConflictAsks++;
            return ValueTask.FromResult(Conflicts.Dequeue());
        }

        public ValueTask<ErrorChoice> AskError(ErrorQuestion question, CancellationToken cancellationToken) =>
            ValueTask.FromResult(OnError?.Invoke() ?? Errors.Dequeue());
    }
}
