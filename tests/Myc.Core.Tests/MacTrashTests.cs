using Myc.Core.Platform;

namespace Myc.Core.Tests;

public class MacTrashTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("myc-").FullName;

    [Fact]
    public void Moves_a_file_to_trash_and_leaves_the_folder()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        string file = Path.Combine(_root, "notes.md");
        File.WriteAllText(file, "hello");
        string target = Path.Combine(_root, "real.txt");
        File.WriteAllText(target, "stay");
        string link = Path.Combine(_root, "link");
        File.CreateSymbolicLink(link, target);

        var trash = new MacTrash();
        TrashMove fileMove = trash.MoveToTrash(file);
        TrashMove linkMove = trash.MoveToTrash(link);

        Assert.Null(fileMove.Error);
        Assert.Null(linkMove.Error);
        Assert.False(File.Exists(file));
        Assert.False(File.Exists(link));
        Assert.Equal("stay", File.ReadAllText(target));
        DeleteTrashed(fileMove.TrashedPath);
        DeleteTrashed(linkMove.TrashedPath);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static void DeleteTrashed(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return;
        }

        File.Delete(path);
    }
}
