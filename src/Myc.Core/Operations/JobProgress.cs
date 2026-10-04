namespace Myc.Core.Operations;

public enum JobPhase
{
    Scanning,
    Copying,
}

/// <summary>One progress snapshot. The UI renders it; the job never touches a view.</summary>
public readonly record struct JobProgress(
    string CurrentItem,
    int ItemsDone,
    int ItemsTotal,
    long BytesDone,
    long BytesTotal,
    JobPhase Phase);
