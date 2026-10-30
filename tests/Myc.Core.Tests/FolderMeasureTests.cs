using Myc.Core.FileSystem;

namespace Myc.Core.Tests;

public class FolderMeasureTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("myc-map-").FullName;

    [Fact]
    public void A_folder_is_the_sum_of_its_files()
    {
        Directory.CreateDirectory(Path.Combine(_root, "photos"));
        File.WriteAllText(Path.Combine(_root, "photos", "reef.jpg"), new string('a', 400));
        File.WriteAllText(Path.Combine(_root, "notes.md"), new string('b', 100));

        FolderMeasureResult result = FolderMeasure.Scan(_root, CancellationToken.None);

        Assert.Equal(500, result.Total);
        Assert.Equal(0, result.Skipped);
        Assert.Equal("photos", result.Children[0].Name);
        Assert.Equal(400, result.Children[0].Bytes);
        Assert.True(result.Children[0].Directory);
        Assert.Contains(result.Children, child => child.Name == "notes.md" && child.Bytes == 100 && !child.Directory);
    }

    [Fact]
    public void A_symlink_is_not_followed()
    {
        string outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "secret.bin"), new string('x', 5_000));
        string shown = Path.Combine(_root, "shown");
        Directory.CreateDirectory(shown);
        File.WriteAllText(Path.Combine(shown, "keep.txt"), "yes");
        Directory.CreateSymbolicLink(Path.Combine(shown, "jump"), outside);

        FolderMeasureResult result = FolderMeasure.Scan(shown, CancellationToken.None);

        FolderChild link = Assert.Single(result.Children, child => child.Name == "jump");
        Assert.Equal(0, link.Bytes);
        Assert.Equal(3, result.Total);
    }

    [Fact]
    public void A_missing_folder_counts_as_skipped()
    {
        FolderMeasureResult result = FolderMeasure.Scan(Path.Combine(_root, "missing"), CancellationToken.None);

        Assert.Equal(1, result.Skipped);
        Assert.Empty(result.Children);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
