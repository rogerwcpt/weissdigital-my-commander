using Myc.Core.Sorting;

namespace Myc.Core.FileSystem;

public sealed class LocalFileSystem : IFileSystem
{
    public DirectoryListing List(string directory, bool showHidden)
    {
        string full = Path.GetFullPath(directory);
        if (!Directory.Exists(full))
        {
            return new DirectoryListing(full, [], "Directory not found.");
        }

        try
        {
            List<FileEntry> entries = [];
            foreach (string path in Directory.EnumerateFileSystemEntries(full))
            {
                FileEntry entry = Read(path);
                if (!showHidden && entry.IsHidden)
                {
                    continue;
                }

                entries.Add(entry);
            }

            FileEntry? parent = ParentOf(full);
            if (parent is not null)
            {
                entries.Add(parent);
            }

            return new DirectoryListing(full, EntrySorter.Sort(entries), null);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            FileEntry? parent = ParentOf(full);
            IReadOnlyList<FileEntry> entries = parent is null ? [] : [parent];
            return new DirectoryListing(full, entries, exception.Message);
        }
    }

    private static FileEntry? ParentOf(string directory)
    {
        string? parent = Path.GetDirectoryName(directory);
        if (string.IsNullOrEmpty(parent))
        {
            return null;
        }

        return new FileEntry
        {
            Name = "..",
            FullPath = parent,
            Kind = FileKind.Directory,
            IsParent = true,
        };
    }

    private static FileEntry Read(string path)
    {
        string name = Path.GetFileName(path);
        bool hidden = name.StartsWith('.');
        string? linkTarget = LinkTarget(path);
        if (linkTarget is not null)
        {
            return new FileEntry
            {
                Name = name,
                FullPath = path,
                Kind = FileKind.Symlink,
                SymlinkTarget = linkTarget,
                PointsAtDirectory = TargetIsDirectory(path),
                IsHidden = hidden,
                Modified = TryModified(path),
                Permissions = TryMode(path),
            };
        }

        if (Directory.Exists(path))
        {
            return new FileEntry
            {
                Name = name,
                FullPath = path,
                Kind = FileKind.Directory,
                IsHidden = hidden,
                Modified = TryModified(path),
                Permissions = TryMode(path),
            };
        }

        if (File.Exists(path))
        {
            long? size = null;
            try
            {
                size = new FileInfo(path).Length;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return new FileEntry
            {
                Name = name,
                FullPath = path,
                Kind = FileKind.File,
                Size = size,
                IsHidden = hidden,
                Modified = TryModified(path),
                Permissions = TryMode(path),
            };
        }

        return new FileEntry
        {
            Name = name,
            FullPath = path,
            Kind = FileKind.Other,
            IsHidden = hidden,
        };
    }

    private static string? LinkTarget(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget ?? new DirectoryInfo(path).LinkTarget;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool TargetIsDirectory(string path)
    {
        try
        {
            FileSystemInfo? target = File.ResolveLinkTarget(path, returnFinalTarget: false);
            // FileInfo.Attributes is -1 when the target is missing, and that value has the
            // directory bit set. Directory.Exists is false for a broken link and for a file.
            return target is not null && Directory.Exists(target.FullName);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static DateTimeOffset? TryModified(string path)
    {
        try
        {
            return new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static UnixFileMode? TryMode(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return null;
            }

            return File.GetUnixFileMode(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return null;
        }
    }
}
