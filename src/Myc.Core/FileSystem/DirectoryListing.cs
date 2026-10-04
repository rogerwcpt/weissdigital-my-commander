namespace Myc.Core.FileSystem;

/// <summary>
/// A directory the panel can show. <see cref="Error"/> is set when the directory cannot be
/// read; <see cref="Entries"/> still contains <c>..</c> when a parent exists, so the user can leave.
/// </summary>
public sealed record DirectoryListing(string Directory, IReadOnlyList<FileEntry> Entries, string? Error);
