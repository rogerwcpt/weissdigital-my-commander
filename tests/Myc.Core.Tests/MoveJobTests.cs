using Myc.Core.Operations;

namespace Myc.Core.Tests;

public class MoveJobTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("myc-").FullName;
    private readonly ScriptedCallbacks _callbacks = new();

    [Fact]
    public async Task Same_volume_renames_a_file_and_a_directory()
    {
        string source = Path.Combine(_root, "src");
        string destination = Path.Combine(_root, "dst");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(source, "notes.md"), "hello");
        Directory.CreateDirectory(Path.Combine(source, "folder"));
        File.WriteAllText(Path.Combine(source, "folder", "inside.txt"), "in");

        JobResult result = await new MoveJob(
            [Path.Combine(source, "notes.md"), Path.Combine(source, "folder")],
            destination).RunAsync(null, _callbacks, CancellationToken.None);

        Assert.Equal(JobStatus.Completed, result.Status);
        Assert.Equal("hello", File.ReadAllText(Path.Combine(destination, "notes.md")));
        Assert.Equal("in", File.ReadAllText(Path.Combine(destination, "folder", "inside.txt")));
        Assert.False(File.Exists(Path.Combine(source, "notes.md")));
        Assert.False(Directory.Exists(Path.Combine(source, "folder")));
    }

    [Fact]
    public async Task A_symlink_is_moved_and_the_target_stays()
    {
        string destination = Path.Combine(_root, "dst");
        Directory.CreateDirectory(destination);
        string target = Path.Combine(_root, "real.txt");
        File.WriteAllText(target, "x");
        File.CreateSymbolicLink(Path.Combine(_root, "link"), target);

        JobResult result = await new MoveJob([Path.Combine(_root, "link")], destination)
            .RunAsync(null, _callbacks, CancellationToken.None);

        Assert.Equal(JobStatus.Completed, result.Status);
        Assert.Equal("x", File.ReadAllText(target));
        Assert.True(File.GetAttributes(Path.Combine(destination, "link")).HasFlag(FileAttributes.ReparsePoint));
        Assert.False(File.Exists(Path.Combine(_root, "link")));
    }

    [Fact]
    public async Task Refuses_to_move_a_folder_into_itself()
    {
        string folder = Path.Combine(_root, "folder");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "notes.md"), "hello");

        JobResult result = await new MoveJob([folder], folder).RunAsync(null, _callbacks, CancellationToken.None);

        Assert.Equal(JobStatus.Refused, result.Status);
        Assert.Equal("hello", File.ReadAllText(Path.Combine(folder, "notes.md")));
    }

    [Fact]
    public async Task Same_folder_offers_a_duplicate_and_keeps_the_original()
    {
        File.WriteAllText(Path.Combine(_root, "notes.md"), "hello");

        JobResult refused = await new MoveJob([Path.Combine(_root, "notes.md")], _root)
            .RunAsync(null, _callbacks, CancellationToken.None);

        Assert.Equal(["notes copy.md"], refused.DuplicateNames);
        Assert.True(File.Exists(Path.Combine(_root, "notes.md")));

        JobResult copied = await new MoveJob([Path.Combine(_root, "notes.md")], _root, duplicateInPlace: true)
            .RunAsync(null, _callbacks, CancellationToken.None);

        Assert.Equal(JobStatus.Completed, copied.Status);
        Assert.Equal("hello", File.ReadAllText(Path.Combine(_root, "notes.md")));
        Assert.Equal("hello", File.ReadAllText(Path.Combine(_root, "notes copy.md")));
    }

    [Fact]
    public async Task Copy_then_delete_removes_only_what_was_copied()
    {
        string source = Path.Combine(_root, "src");
        string destination = Path.Combine(_root, "dst");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(source, "keep.md"), "new");
        File.WriteAllText(Path.Combine(source, "skip.md"), "new");
        File.WriteAllText(Path.Combine(destination, "skip.md"), "old");
        _callbacks.Conflicts.Enqueue(ConflictChoice.Skip);

        JobResult result = await new MoveJob(
            [Path.Combine(source, "keep.md"), Path.Combine(source, "skip.md")],
            destination,
            copyThenDelete: true).RunAsync(null, _callbacks, CancellationToken.None);

        Assert.Equal(JobStatus.Completed, result.Status);
        Assert.Equal("new", File.ReadAllText(Path.Combine(destination, "keep.md")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(destination, "skip.md")));
        Assert.False(File.Exists(Path.Combine(source, "keep.md")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(source, "skip.md")));
    }

    [Fact]
    public void Two_paths_in_one_temp_folder_are_the_same_volume()
    {
        Assert.True(Volumes.Same(_root, Path.Combine(_root, "other")));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class ScriptedCallbacks : IJobCallbacks
    {
        public Queue<ConflictChoice> Conflicts { get; } = new();

        public ValueTask<ConflictChoice> AskConflict(ConflictQuestion question, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Conflicts.Dequeue());

        public ValueTask<ErrorChoice> AskError(ErrorQuestion question, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ErrorChoice.Cancel);
    }
}
