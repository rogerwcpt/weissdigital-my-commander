using Myc.Core.FileSystem;

namespace Myc.Core.Tests;

public class FileChangeTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("myc-").FullName;
    private readonly LocalFileSystem _files = new();

    [Fact]
    public void Rename_moves_a_file_and_reports_the_new_name()
    {
        File.WriteAllText(Path.Combine(_root, "notes.md"), "x");

        FileChange change = _files.Rename(_root, "notes.md", "done.md");

        Assert.Null(change.Error);
        Assert.Equal("done.md", change.SelectedName);
        Assert.False(File.Exists(Path.Combine(_root, "notes.md")));
        Assert.Equal("x", File.ReadAllText(Path.Combine(_root, "done.md")));
    }

    [Fact]
    public void Case_only_rename_keeps_the_new_casing()
    {
        File.WriteAllText(Path.Combine(_root, "readme"), "x");

        FileChange change = _files.Rename(_root, "readme", "README");

        Assert.Null(change.Error);
        Assert.Equal("README", change.SelectedName);
        Assert.Contains("README", Names());
        Assert.DoesNotContain("readme", Names());
        Assert.DoesNotContain(Names(), name => name.StartsWith(".myc-", StringComparison.Ordinal));
    }

    [Fact]
    public void Rename_treats_a_decomposed_name_as_the_file_already_there()
    {
        File.WriteAllText(Path.Combine(_root, "cafe\u0301.txt"), "stay");
        File.WriteAllText(Path.Combine(_root, "other.txt"), "x");

        FileChange taken = _files.Rename(_root, "other.txt", "caf\u00e9.txt");
        FileChange spelling = _files.Rename(_root, "cafe\u0301.txt", "caf\u00e9.txt");

        Assert.Equal("That name is already used.", taken.Error);
        Assert.Null(spelling.Error);
        Assert.Equal("stay", File.ReadAllText(Path.Combine(_root, "cafe\u0301.txt")));
        Assert.Equal("x", File.ReadAllText(Path.Combine(_root, "other.txt")));
    }

    [Fact]
    public void Rename_refuses_a_name_that_is_already_used()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "b");

        FileChange change = _files.Rename(_root, "a.txt", "b.txt");

        Assert.Equal("That name is already used.", change.Error);
        Assert.Equal("a", File.ReadAllText(Path.Combine(_root, "a.txt")));
    }

    [Fact]
    public void Rename_refuses_a_slash_and_a_missing_item()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");

        Assert.Equal("A name can't contain /.", _files.Rename(_root, "a.txt", "a/b").Error);
        Assert.Equal("That item is no longer there.", _files.Rename(_root, "gone", "x").Error);
        Assert.True(File.Exists(Path.Combine(_root, "a.txt")));
    }

    [Fact]
    public void Rename_moves_a_symlink_and_leaves_the_target()
    {
        string target = Path.Combine(_root, "real.txt");
        File.WriteAllText(target, "x");
        File.CreateSymbolicLink(Path.Combine(_root, "link"), target);

        FileChange change = _files.Rename(_root, "link", "renamed");

        Assert.Equal("renamed", change.SelectedName);
        Assert.Equal("x", File.ReadAllText(target));
        Assert.Equal(FileKind.Symlink, _files.List(_root, showHidden: false).Entries.Single(entry => entry.Name == "renamed").Kind);
    }

    [Fact]
    public void Mkdir_creates_intermediate_folders_and_selects_the_first()
    {
        FileChange change = _files.CreateDirectory(_root, "a/b/c");

        Assert.Null(change.Error);
        Assert.Equal("a", change.SelectedName);
        Assert.True(Directory.Exists(Path.Combine(_root, "a", "b", "c")));
    }

    [Fact]
    public void Mkdir_refuses_an_existing_name_a_file_in_the_way_and_an_escape()
    {
        Directory.CreateDirectory(Path.Combine(_root, "taken"));
        File.WriteAllText(Path.Combine(_root, "file"), "x");
        string outside = Path.Combine(Directory.GetParent(_root)!.FullName, "myc-escaped");

        Assert.Equal("That name is already used.", _files.CreateDirectory(_root, "taken").Error);
        Assert.Equal("A file is in the way.", _files.CreateDirectory(_root, "file/child").Error);
        Assert.Equal("That name is reserved.", _files.CreateDirectory(_root, "../myc-escaped").Error);
        Assert.False(Directory.Exists(outside));
        Assert.Equal("x", File.ReadAllText(Path.Combine(_root, "file")));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string[] Names() =>
        Directory.EnumerateFileSystemEntries(_root).Select(Path.GetFileName).ToArray()!;
}
