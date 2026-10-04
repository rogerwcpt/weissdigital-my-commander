using Myc.Core.Operations;
using Myc.Core.Platform;

namespace Myc.Core.Tests;

public class DeleteJobTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("myc-").FullName;
    private readonly ScriptedCallbacks _callbacks = new();

    [Fact]
    public async Task Permanent_delete_removes_a_tree_and_leaves_a_link_target()
    {
        string folder = Path.Combine(_root, "folder");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "notes.md"), "hello");
        string target = Path.Combine(_root, "real.txt");
        File.WriteAllText(target, "stay");
        File.CreateSymbolicLink(Path.Combine(folder, "link"), target);

        JobResult result = await new DeleteJob([folder], permanent: true)
            .RunAsync(null, _callbacks, CancellationToken.None);

        Assert.Equal(JobStatus.Completed, result.Status);
        Assert.False(Directory.Exists(folder));
        Assert.Equal("stay", File.ReadAllText(target));
    }

    [Fact]
    public async Task Cancel_stops_before_the_next_item()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "b");
        using CancellationTokenSource cancel = new();
        var progress = new ProgressList();
        progress.OnReport = item =>
        {
            if (item.Phase == JobPhase.Copying && item.ItemsDone >= 1)
            {
                cancel.Cancel();
            }
        };

        JobResult result = await new DeleteJob(
            [Path.Combine(_root, "a.txt"), Path.Combine(_root, "b.txt")],
            permanent: true).RunAsync(progress, _callbacks, cancel.Token);

        Assert.Equal(JobStatus.Cancelled, result.Status);
        Assert.False(File.Exists(Path.Combine(_root, "a.txt")));
        Assert.True(File.Exists(Path.Combine(_root, "b.txt")));
    }

    [Fact]
    public async Task Trash_uses_the_platform_call_once_per_selected_item()
    {
        string folder = Path.Combine(_root, "folder");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "inside.txt"), "x");
        File.WriteAllText(Path.Combine(_root, "notes.md"), "y");
        var trash = new RecordingTrash();

        JobResult result = await new DeleteJob(
            [folder, Path.Combine(_root, "notes.md")],
            permanent: false,
            trash).RunAsync(null, _callbacks, CancellationToken.None);

        Assert.Equal(JobStatus.Completed, result.Status);
        Assert.Equal([folder, Path.Combine(_root, "notes.md")], trash.Paths);
        Assert.True(File.Exists(Path.Combine(folder, "inside.txt")));
    }

    [Fact]
    public async Task A_missing_item_fails_before_anything_is_removed()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
        var trash = new RecordingTrash();

        JobResult result = await new DeleteJob(
            [Path.Combine(_root, "a.txt"), Path.Combine(_root, "missing.txt")],
            permanent: false,
            trash).RunAsync(null, _callbacks, CancellationToken.None);

        Assert.Equal(JobStatus.Failed, result.Status);
        Assert.Empty(trash.Paths);
        Assert.True(File.Exists(Path.Combine(_root, "a.txt")));
    }

    [Fact]
    public async Task A_trash_failure_can_be_skipped()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "b");
        var trash = new RecordingTrash { Fail = "locked" };
        _callbacks.Errors.Enqueue(ErrorChoice.Skip);
        _callbacks.Errors.Enqueue(ErrorChoice.Skip);

        JobResult result = await new DeleteJob(
            [Path.Combine(_root, "a.txt"), Path.Combine(_root, "b.txt")],
            permanent: false,
            trash).RunAsync(null, _callbacks, CancellationToken.None);

        Assert.Equal(JobStatus.Completed, result.Status);
        Assert.Equal(2, result.Skipped);
        Assert.True(File.Exists(Path.Combine(_root, "a.txt")));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class ProgressList : IProgress<JobProgress>
    {
        public Action<JobProgress>? OnReport { get; set; }

        public void Report(JobProgress value) => OnReport?.Invoke(value);
    }

    private sealed class ScriptedCallbacks : IJobCallbacks
    {
        public Queue<ErrorChoice> Errors { get; } = new();

        public ValueTask<ConflictChoice> AskConflict(ConflictQuestion question, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConflictChoice.Cancel);

        public ValueTask<ErrorChoice> AskError(ErrorQuestion question, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Errors.Dequeue());
    }

    private sealed class RecordingTrash : ITrash
    {
        public List<string> Paths { get; } = [];

        public string? Fail { get; set; }

        public TrashMove MoveToTrash(string path)
        {
            Paths.Add(path);
            return Fail is null ? new TrashMove(null, null) : new TrashMove(Fail, null);
        }
    }
}
