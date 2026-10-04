namespace Myc.Core.Operations;

public enum ConflictChoice
{
    Overwrite,
    Skip,
    Rename,
    OverwriteAll,
    SkipAll,
    Cancel,
}

public enum ErrorChoice
{
    Retry,
    Skip,
    Cancel,
}

public sealed record ConflictQuestion(string Name, string DestinationPath);

public sealed record ErrorQuestion(string Name, string Message);

/// <summary>
/// Answers from the UI. Implementations may show a dialog; the job only sees the choice.
/// </summary>
public interface IJobCallbacks
{
    ValueTask<ConflictChoice> AskConflict(ConflictQuestion question, CancellationToken cancellationToken);

    ValueTask<ErrorChoice> AskError(ErrorQuestion question, CancellationToken cancellationToken);
}
