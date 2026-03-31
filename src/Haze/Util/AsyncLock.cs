using System;
using System.Threading;
using System.Threading.Tasks;

namespace Haze.Util;

public sealed class AsyncLock
{
    private readonly SemaphoreSlim _semaphore = new(1);

    public async ValueTask Wait() => await _semaphore.WaitAsync();

    public async ValueTask Release() => _semaphore.Release(1);

    public async ValueTask<IAsyncDisposable> EnterScope()
    {
        await Wait();
        return new Scope(this);
    }

    private sealed class Scope(AsyncLock @lock) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await @lock.Release();
    }
}
