namespace Myc.Core.FileSystem;

public interface IFileSystem
{
    DirectoryListing List(string directory, bool showHidden);
}
