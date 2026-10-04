namespace Myc.Core.FileSystem;

/// <summary>
/// One row in a panel. Names are kept as the file system returns them (NFD on macOS);
/// callers compare names with the same form, and normalize only when showing or matching text.
/// </summary>
public sealed record FileEntry
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public required FileKind Kind { get; init; }

    public long? Size { get; init; }

    public DateTimeOffset? Modified { get; init; }

    public bool IsHidden { get; init; }

    public UnixFileMode? Permissions { get; init; }

    public string? SymlinkTarget { get; init; }

    /// <summary>True when this symlink's immediate target is a directory. Never walks the chain.</summary>
    public bool PointsAtDirectory { get; init; }

    /// <summary>The synthetic parent row. It cannot be marked or operated on.</summary>
    public bool IsParent { get; init; }

    public bool IsContainer => Kind == FileKind.Directory || PointsAtDirectory;
}
