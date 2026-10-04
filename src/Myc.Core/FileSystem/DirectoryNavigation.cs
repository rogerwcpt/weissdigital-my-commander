namespace Myc.Core.FileSystem;

/// <summary>Where a panel should go, and which name to put the cursor on.</summary>
public readonly record struct DirectoryMove(string Directory, string? SelectName);

public static class DirectoryNavigation
{
    /// <summary>
    /// Right arrow or Enter on a directory. <c>..</c> goes up and selects the folder being left.
    /// A file is not a move.
    /// </summary>
    public static DirectoryMove? Enter(FileEntry entry, string currentDirectory)
    {
        if (entry.IsParent)
        {
            return Up(currentDirectory);
        }

        return entry.IsContainer ? new DirectoryMove(entry.FullPath, null) : null;
    }

    /// <summary>Left arrow. At the filesystem root there is nowhere to go.</summary>
    public static DirectoryMove? Up(string currentDirectory)
    {
        string? parent = Path.GetDirectoryName(currentDirectory);
        if (string.IsNullOrEmpty(parent))
        {
            return null;
        }

        return new DirectoryMove(parent, Path.GetFileName(currentDirectory));
    }
}
