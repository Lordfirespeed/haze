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
