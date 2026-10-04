namespace Myc.Core.FileSystem;

public interface IFileSystem
{
    DirectoryListing List(string directory, bool showHidden);

    FileChange Rename(string directory, string name, string newName);

    /// <summary>
    /// Creates a directory inside <paramref name="directory"/>. Slashes in <paramref name="relativeName"/>
    /// create intermediate folders. The selected name is the first segment, the row that appears here.
    /// </summary>
    FileChange CreateDirectory(string directory, string relativeName);
}
