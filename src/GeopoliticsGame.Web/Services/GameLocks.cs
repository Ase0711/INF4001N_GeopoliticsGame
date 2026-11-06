using System.Collections.Concurrent;

namespace GeopoliticsGame.Web.Services;

public sealed class GameLocks
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(string gameId, CancellationToken ct = default)
    {
        var semaphore = _locks.GetOrAdd(gameId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        return new Releaser(semaphore);
    }

    private sealed class Releaser : IDisposable
    {
        private SemaphoreSlim? _semaphore;
        public Releaser(SemaphoreSlim semaphore) => _semaphore = semaphore;
        public void Dispose() => Interlocked.Exchange(ref _semaphore, null)?.Release();
    }
}
