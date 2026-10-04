namespace Myc.Core.FileSystem;

/// <summary>Result of a rename or mkdir. <see cref="Error"/> is null when it worked.</summary>
public readonly record struct FileChange(string? SelectedName, string? Error)
{
    public bool Succeeded => Error is null;
}
