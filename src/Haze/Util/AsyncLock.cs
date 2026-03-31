using System;
using System.Threading;
using System.Threading.Tasks;

namespace Haze.Util;

public sealed class AsyncLock
{
    private readonly SemaphoreSlim _semaphore = new(1);

    public async ValueTask Wait(CancellationToken ct = default) => await _semaphore.WaitAsync(ct);

    public ValueTask Release(CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        _semaphore.Release(1);
        return ValueTask.CompletedTask;
    }

    public async ValueTask<IAsyncDisposable> EnterScope(CancellationToken ct = default)
    {
        await Wait(ct);
        return new Scope(this);
    }

    private sealed class Scope(AsyncLock @lock) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await @lock.Release();
    }
}
