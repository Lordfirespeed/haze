using System;
using System.Threading;
using System.Threading.Tasks;
using Haze.Util;
using SteamKit2;

namespace Haze.Steam;

public static class CallbackManagerExtensions
{
    #region `Subscribe` methods for `async Task`/`async ValueTask` subscribers

    private sealed class CancellingDisposable(CancellationTokenSource cts, IDisposable? disposable = null) : IDisposable
    {
        private bool _disposed = false;
        public CancellationToken Token => cts.Token;

        public void Dispose()
        {
            if (_disposed) return;
            cts.Cancel();
            disposable?.Dispose();
            _disposed = true;
        }
    }

    public static IDisposable Subscribe<TCallback>(
        this CallbackManager manager,
        Func<TCallback, Task> callbackFunc
    )
        where TCallback : CallbackMsg
    {
        var cts = new CancellationTokenSource();
        var disposable = manager.Subscribe<TCallback>(callback => {
            Task.Run(async () => await callbackFunc(callback), cts.Token);
        });
        return new CancellingDisposable(cts, disposable);
    }

    public static IDisposable Subscribe<TCallback>(
        this CallbackManager manager,
        Func<TCallback, ValueTask> callbackFunc
    )
        where TCallback : CallbackMsg
    {
        return Subscribe<TCallback>(manager, callback => callbackFunc(callback).AsTask());
    }

    public static IDisposable Subscribe<TCallback>(
        this CallbackManager manager,
        JobID jobId,
        Func<TCallback, Task> callbackFunc
    )
        where TCallback : CallbackMsg
    {
        var cts = new CancellationTokenSource();
        var disposable = manager.Subscribe<TCallback>(callback => {
            Task.Run(async () => await callbackFunc(callback), cts.Token);
        });
        return new CancellingDisposable(cts, disposable);
    }

    public static IDisposable Subscribe<TCallback>(
        this CallbackManager manager,
        JobID jobId,
        Func<TCallback, ValueTask> callbackFunc
    )
        where TCallback : CallbackMsg
    {
        return Subscribe<TCallback>(manager, jobId, callback => callbackFunc(callback).AsTask());
    }

    #endregion

    #region `Run` loop methods

    public static void RunForever(
        this CallbackManager manager,
        TimeSpan interval,
        CancellationToken cancellationToken = default
    ) {
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            manager.RunWaitCallbacks(interval);
        }
        // ReSharper disable once FunctionNeverReturns
    }

    public static async Task RunForeverAsync(
        this CallbackManager manager,
        CancellationToken cancellationToken = default
    ) {
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            await manager.RunWaitCallbackAsync(cancellationToken);
        }
        // ReSharper disable once FunctionNeverReturns
    }

    public static async Task RunUntil(
        this CallbackManager manager,
        Task untilTask,
        CancellationToken cancellationToken = default
    ) {
        cancellationToken.ThrowIfCancellationRequested();
        using var untilTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var runTask = Task.Run(
            async () => await manager.RunForeverAsync(cancellationToken: untilTokenSource.Token),
            untilTokenSource.Token
        );
        try {
            await untilTask.WaitAsync(cancellationToken);
        }
        finally {
            await untilTokenSource.CancelAsync();
            await runTask.IgnoreCancellationBy(untilTokenSource.Token);
        }
    }

    #endregion
}
