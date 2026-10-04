namespace Myc.Core.Platform;

public readonly record struct TrashMove(string? Error, string? TrashedPath);

/// <summary>Moves one item to the system Trash. The path is the item itself, never a followed link.</summary>
public interface ITrash
{
    TrashMove MoveToTrash(string path);
}
