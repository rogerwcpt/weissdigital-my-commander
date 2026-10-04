using Myc.App.Views;
using Myc.Core.Operations;
using Terminal.Gui.App;

namespace Myc.App.Input;

/// <summary>
/// Runs a job off the UI thread. The progress dialog waits half a second so a small copy does not flash.
/// Questions are asked on the UI thread; views are only touched there.
/// </summary>
internal sealed class JobRunner
{
    private readonly IApplication _app;
    private readonly Func<bool> _confirmOverwrite;
    private int _running;

    public JobRunner(IApplication app, Func<bool> confirmOverwrite)
    {
        _app = app;
        _confirmOverwrite = confirmOverwrite;
    }

    public bool IsRunning => _running > 0;

    public void Start(IFileJob job, string title, Action<JobResult> completed)
    {
        if (_running > 0)
        {
            return;
        }

        _running++;
        var cancel = new CancellationTokenSource();
        bool open = false;
        bool finished = false;
        var dialog = new ProgressDialog(title, () => cancel.Cancel());
        var callbacks = new DialogCallbacks(_app, _confirmOverwrite);
        Task<JobResult> task = Task.Run(() => job.RunAsync(new UiProgress(_app, dialog.Apply), callbacks, cancel.Token));

        void Complete(Task<JobResult> done)
        {
            if (finished)
            {
                return;
            }

            finished = true;
            _running--;
            JobResult result = done.IsFaulted
                ? JobResult.Fail(done.Exception?.GetBaseException().Message ?? "The operation failed.")
                : done.Result;
            completed(result);
            cancel.Dispose();
        }

        object? clock = _app.AddTimeout(TimeSpan.FromSeconds(1), () =>
        {
            if (finished)
            {
                return false;
            }

            if (open)
            {
                dialog.Tick();
            }

            return true;
        });

        object? timeout = _app.AddTimeout(TimeSpan.FromMilliseconds(500), () =>
        {
            if (finished || task.IsCompleted)
            {
                return false;
            }

            open = true;
            _app.Run(dialog);
            open = false;
            if (task.IsCompleted)
            {
                if (clock is not null)
                {
                    _app.RemoveTimeout(clock);
                }

                Complete(task);
            }

            return false;
        });

        _ = task.ContinueWith(done =>
        {
            _app.Invoke(() =>
            {
                if (timeout is not null)
                {
                    _app.RemoveTimeout(timeout);
                }

                if (clock is not null)
                {
                    _app.RemoveTimeout(clock);
                }
                if (open)
                {
                    dialog.RequestStop();
                    return;
                }

                Complete(done);
            });
        }, TaskScheduler.Default);
    }

    private sealed class UiProgress(IApplication app, Action<JobProgress> apply) : IProgress<JobProgress>
    {
        public void Report(JobProgress value) => app.Invoke(() => apply(value));
    }

    private sealed class DialogCallbacks(IApplication app, Func<bool> confirmOverwrite) : IJobCallbacks
    {
        public ValueTask<ConflictChoice> AskConflict(ConflictQuestion question, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask<ConflictChoice>(ConflictChoice.Cancel);
            }

            if (!confirmOverwrite())
            {
                return new ValueTask<ConflictChoice>(ConflictChoice.Overwrite);
            }

            var answer = new TaskCompletionSource<ConflictChoice>(TaskCreationOptions.RunContinuationsAsynchronously);
            app.Invoke(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    answer.SetResult(ConflictChoice.Cancel);
                    return;
                }

                answer.SetResult(ConflictPrompt.Ask(app, question));
            });
            return new ValueTask<ConflictChoice>(answer.Task);
        }

        public ValueTask<ErrorChoice> AskError(ErrorQuestion question, CancellationToken cancellationToken)
        {
            var answer = new TaskCompletionSource<ErrorChoice>(TaskCreationOptions.RunContinuationsAsynchronously);
            app.Invoke(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    answer.SetResult(ErrorChoice.Cancel);
                    return;
                }

                answer.SetResult(ErrorPrompt.Ask(app, question));
            });
            return new ValueTask<ErrorChoice>(answer.Task);
        }
    }
}
