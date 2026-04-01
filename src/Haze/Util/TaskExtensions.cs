using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Haze.Util;

public static class TaskExtensions
{
    public static async Task IgnoreCancellation(this Task @this)
    {
        try { await @this; }
        catch (Exception exc) when (exc is TaskCanceledException or OperationCanceledException) { }
    }

    public static Task WithAggregatedExceptions(this Task @this)
    {
        return @this
            .ContinueWith((task) =>
                {
                    if (task.Exception is null) return task;
                    if (task.Exception.InnerExceptions.Count <= 1 &&
                        task.Exception.InnerException is not AggregateException) return task;
                    return Task.FromException(task.Exception.Flatten());
                },
                TaskContinuationOptions.OnlyOnFaulted
            )
            .Unwrap();
    }

    /**
     * <summary>
     * Creates a task that will complete when all of the <see cref="Task"/> objects in an enumerable collection have
     * completed.
     * If any task does not run to completion (i.e. it faults or is cancelled), the provided
     * <see cref="CancellationTokenSource"/> is cancelled.
     * </summary>
     */
    public static Task Group(IEnumerable<Task> tasks, CancellationTokenSource cts)
    {
        var continuations = tasks.Select(
            (task) => task.ContinueWith(
                (task) => {
                    cts.Cancel();
                    return task;
                },
                cts.Token,
                TaskContinuationOptions.NotOnRanToCompletion,
                TaskScheduler.Default
            ).Unwrap().IgnoreCancellation()
        );

        return Task.WhenAll(continuations);
    }
}
