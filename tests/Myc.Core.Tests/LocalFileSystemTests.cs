using Myc.Core.FileSystem;

namespace Myc.Core.Tests;

public class LocalFileSystemTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("myc-").FullName;
    private readonly LocalFileSystem _files = new();

    [Fact]
    public void Lists_parent_then_directories_then_files_and_hides_dotfiles()
    {
        Directory.CreateDirectory(Path.Combine(_root, "folder"));
        File.WriteAllText(Path.Combine(_root, "file10.txt"), "x");
        File.WriteAllText(Path.Combine(_root, "file2.txt"), "x");
        File.WriteAllText(Path.Combine(_root, ".secret"), "x");

        DirectoryListing listing = _files.List(_root, showHidden: false);

        Assert.Null(listing.Error);
        Assert.Equal(["..", "folder", "file2.txt", "file10.txt"], listing.Entries.Select(entry => entry.Name).ToArray());
        Assert.Equal(1, listing.Entries.Single(entry => entry.Name == "file2.txt").Size);
    }

    [Fact]
    public void Show_hidden_includes_dotfiles()
    {
        File.WriteAllText(Path.Combine(_root, ".secret"), "x");

        DirectoryListing listing = _files.List(_root, showHidden: true);

        Assert.Contains(listing.Entries, entry => entry.Name == ".secret" && entry.IsHidden);
    }

    [Fact]
    public void Keeps_a_symlink_as_a_link_and_does_not_list_the_target_contents()
    {
        string target = Path.Combine(_root, "real");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "inside.txt"), "x");
        Directory.CreateSymbolicLink(Path.Combine(_root, "alias"), target);

        DirectoryListing listing = _files.List(_root, showHidden: false);

        FileEntry link = Assert.Single(listing.Entries, entry => entry.Name == "alias");
        Assert.Equal(FileKind.Symlink, link.Kind);
        Assert.True(link.PointsAtDirectory);
        Assert.DoesNotContain(listing.Entries, entry => entry.Name == "inside.txt");
    }

    [Fact]
    public void Broken_symlink_is_still_listed()
    {
        File.CreateSymbolicLink(Path.Combine(_root, "missing"), Path.Combine(_root, "nope"));

        DirectoryListing listing = _files.List(_root, showHidden: false);

        FileEntry link = Assert.Single(listing.Entries, entry => entry.Name == "missing");
        Assert.Equal(FileKind.Symlink, link.Kind);
        Assert.False(link.PointsAtDirectory);
    }

    [Fact]
    public void Permission_denied_keeps_the_parent_row_and_reports_the_error()
    {
        string locked = Path.Combine(_root, "locked");
        Directory.CreateDirectory(locked);
        SetMode(locked, UnixFileMode.None);
        try
        {
            DirectoryListing listing = _files.List(locked, showHidden: false);

            Assert.NotNull(listing.Error);
            Assert.Equal([".."], listing.Entries.Select(entry => entry.Name).ToArray());
        }
        finally
        {
            SetMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void Missing_directory_is_an_error_without_throwing()
    {
        DirectoryListing listing = _files.List(Path.Combine(_root, "gone"), showHidden: false);

        Assert.NotNull(listing.Error);
        Assert.Empty(listing.Entries);
    }

    [Fact]
    public void Preserves_an_nfd_name()
    {
        string name = "cafe\u0301.txt";
        File.WriteAllText(Path.Combine(_root, name), "x");

        DirectoryListing listing = _files.List(_root, showHidden: false);

        Assert.Contains(listing.Entries, entry => entry.Name == name);
    }

    public void Dispose()
    {
        try
        {
            foreach (string directory in Directory.EnumerateDirectories(_root))
            {
                SetMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
        catch (IOException)
        {
        }

        Directory.Delete(_root, recursive: true);
    }

    private static void SetMode(string path, UnixFileMode mode)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, mode);
        }
    }
}
