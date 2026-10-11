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

    /// <summary>Creates an empty file inside <paramref name="directory"/>. The selected name is that file.</summary>
    FileChange CreateFile(string directory, string name);
}
