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

    public FileChange Rename(string directory, string name, string newName)
    {
        if (name is "." or "..")
        {
            return Fail("Can't rename that.");
        }

        if (EntryNames.ComponentRejection(newName) is { } rejection)
        {
            return Fail(rejection);
        }

        if (EntryNames.Same(name, newName))
        {
            return new FileChange(name, null);
        }

        string full = Path.GetFullPath(directory);
        try
        {
            if (!Occupies(full, name))
            {
                return Fail("That item is no longer there.");
            }

            if (Occupies(full, newName) || (CollidesIgnoringCase(full, name, newName) && !IsCaseSensitive(full)))
            {
                return Fail("That name is already used.");
            }

            string source = Path.Combine(full, name);
            string destination = Path.Combine(full, newName);
            if (EntryNames.EqualIgnoringCase(name, newName))
            {
                MoveCaseOnly(source, destination, full);
            }
            else
            {
                MoveEntry(source, destination);
            }

            return new FileChange(newName, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Fail(exception.Message);
        }
    }

    public FileChange CreateDirectory(string directory, string relativeName)
    {
        if (EntryNames.RelativeDirectoryRejection(relativeName) is { } rejection)
        {
            return Fail(rejection);
        }

        string trimmed = relativeName.Trim().TrimEnd('/');
        string[] parts = trimmed.Split('/');
        string full = Path.GetFullPath(directory);
        string target = Path.GetFullPath(Path.Combine(full, trimmed));
        string prefix = full.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!target.StartsWith(prefix, StringComparison.Ordinal))
        {
            return Fail("Use a name inside this folder.");
        }

        try
        {
            string cursor = full;
            for (int i = 0; i < parts.Length; i++)
            {
                string next = Path.Combine(cursor, parts[i]);
                bool last = i == parts.Length - 1;
                if (last && Directory.Exists(cursor) &&
                    (Occupies(cursor, parts[i]) || (CollidesIgnoringCase(cursor, null, parts[i]) && !IsCaseSensitive(cursor))))
                {
                    return Fail("That name is already used.");
                }

                if (!last && BlocksDirectory(next))
                {
                    return Fail("A file is in the way.");
                }

                cursor = next;
            }

            Directory.CreateDirectory(target);
            return new FileChange(parts[0], null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Fail(exception.Message);
        }
    }

    private static FileChange Fail(string error) => new(null, error);

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

    private static void MoveCaseOnly(string source, string destination, string directory)
    {
        string temp = Path.Combine(directory, ".myc-rename-" + Guid.NewGuid().ToString("N"));
        MoveEntry(source, temp);
        try
        {
            MoveEntry(temp, destination);
        }
        catch
        {
            MoveEntry(temp, source);
            throw;
        }
    }

    private static void MoveEntry(string source, string destination)
    {
        if (HasReparsePoint(source) || !Directory.Exists(source))
        {
            File.Move(source, destination);
            return;
        }

        Directory.Move(source, destination);
    }

    private static bool HasReparsePoint(string path) =>
        File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);

    private static bool BlocksDirectory(string path)
    {
        if (!ExistsEntry(path))
        {
            return false;
        }

        if (HasReparsePoint(path))
        {
            return !Directory.Exists(path);
        }

        return File.Exists(path);
    }

    private static bool ExistsEntry(string path)
    {
        string? parent = Path.GetDirectoryName(path);
        return parent is not null && Occupies(parent, Path.GetFileName(path));
    }

    private static bool Occupies(string directory, string name)
    {
        if (!Directory.Exists(directory))
        {
            return false;
        }

        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            if (EntryNames.Same(Path.GetFileName(path), name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CollidesIgnoringCase(string directory, string? except, string name)
    {
        if (!Directory.Exists(directory))
        {
            return false;
        }

        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            string existing = Path.GetFileName(path);
            if (except is not null && EntryNames.Same(existing, except))
            {
                continue;
            }

            if (EntryNames.EqualIgnoringCase(existing, name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCaseSensitive(string directory)
    {
        string token = Guid.NewGuid().ToString("N");
        string lower = Path.Combine(directory, ".myc-case-" + token);
        string upper = Path.Combine(directory, ".MYC-CASE-" + token.ToUpperInvariant());
        using (File.Create(lower))
        {
        }

        try
        {
            return !File.Exists(upper);
        }
        finally
        {
            File.Delete(lower);
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
