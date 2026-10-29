namespace Myc.Core.Operations;

public enum JobStatus
{
    Completed,
    Cancelled,
    Refused,
    Failed,
}

public sealed record JobResult(
    JobStatus Status,
    string? Message,
    int Copied,
    int Skipped,
    IReadOnlyList<string> DuplicateNames,
    IReadOnlyList<string>? CopiedPaths = null,
    IReadOnlyList<string>? CreatedNames = null)
{
    public IReadOnlyList<string> PathsCopied => CopiedPaths ?? [];

    /// <summary>Top-level names written by a free-name duplicate. Empty for an ordinary copy.</summary>
    public IReadOnlyList<string> NamesCreated => CreatedNames ?? [];

    public static JobResult Done(
        int copied,
        int skipped,
        IReadOnlyList<string>? copiedPaths = null,
        IReadOnlyList<string>? createdNames = null) =>
        new(JobStatus.Completed, null, copied, skipped, [], copiedPaths, createdNames);

    public static JobResult Refuse(string message, IReadOnlyList<string>? duplicateNames = null) =>
        new(JobStatus.Refused, message, 0, 0, duplicateNames ?? []);

    public static JobResult Fail(string message) =>
        new(JobStatus.Failed, message, 0, 0, []);
}
