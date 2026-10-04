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
    IReadOnlyList<string>? CopiedPaths = null)
{
    public IReadOnlyList<string> PathsCopied => CopiedPaths ?? [];

    public static JobResult Done(int copied, int skipped, IReadOnlyList<string>? copiedPaths = null) =>
        new(JobStatus.Completed, null, copied, skipped, [], copiedPaths);

    public static JobResult Refuse(string message, IReadOnlyList<string>? duplicateNames = null) =>
        new(JobStatus.Refused, message, 0, 0, duplicateNames ?? []);

    public static JobResult Fail(string message) =>
        new(JobStatus.Failed, message, 0, 0, []);
}
