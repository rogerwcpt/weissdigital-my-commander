namespace Myc.Core.Operations;

/// <summary>
/// A copy, move, or delete that pre-scans, then runs until it finishes, is cancelled, or asks a question.
/// Call it on a background task. Progress and questions are raised from that task; the UI marshals them.
/// </summary>
public interface IFileJob
{
    Task<JobResult> RunAsync(
        IProgress<JobProgress>? progress,
        IJobCallbacks callbacks,
        CancellationToken cancellationToken = default);
}
