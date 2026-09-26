using System.Collections.Concurrent;
using TRPG.Application.Common.Concurrency;

namespace TRPG.Application.Worlds;

internal sealed class WorldMutationGate : IWorldMutationGate
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();

    public async Task<IAsyncDisposable> Acquire(
        Guid worldId,
        CancellationToken cancellationToken = default
    )
    {
        var gate = _gates.GetOrAdd(worldId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Lease(gate);
    }

    private sealed class Lease(SemaphoreSlim gate) : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                gate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
