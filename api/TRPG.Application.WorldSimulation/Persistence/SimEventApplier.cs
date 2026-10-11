using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.WorldSimulation.Movement;

namespace TRPG.Application.WorldSimulation.Persistence;

internal sealed class SimEventApplier(
    Func<IReadOnlyList<SimEvent>, CancellationToken, Task> write,
    Func<CancellationToken, Task<long>> readMarker,
    int maximumConsecutiveFailures
)
{
    private readonly List<SimEvent> _pending = [];
    private long? _markerBeforeAttempt;
    private int _consecutiveFailures;
    private volatile bool _isBlocked;

    public static SimEventApplier ForWorld(
        Guid worldId,
        IServiceScopeFactory serviceScopeFactory,
        int maximumConsecutiveFailures
    )
    {
        var writer = new SimEventBatchWriter(worldId, serviceScopeFactory);

        return new SimEventApplier(writer.Write, writer.ReadMarker, maximumConsecutiveFailures);
    }

    public bool HasPending => _pending.Count > 0;

    public int PendingCount => _pending.Count;

    public int ConsecutiveFailures => _consecutiveFailures;

    public bool IsBlocked => _isBlocked;

    public bool RetriesExhausted => _consecutiveFailures >= maximumConsecutiveFailures;

    public void Enqueue(IEnumerable<SimEvent> events) => _pending.AddRange(events);

    public async Task<IReadOnlyList<SimEvent>> Commit(CancellationToken cancellationToken)
    {
        try
        {
            if (await HasAlreadyCommitted(cancellationToken))
            {
                return TakeCommitted();
            }

            await write([.. _pending], cancellationToken);

            return TakeCommitted();
        }
        catch
        {
            _consecutiveFailures++;
            _isBlocked = true;
            throw;
        }
    }

    public async Task<IReadOnlyList<SimEvent>> ResolveUncertain(CancellationToken cancellationToken)
    {
        var committed =
            _markerBeforeAttempt is not null && await MarkerAdvanced(cancellationToken)
                ? TakeCommitted()
                : [];
        Discard();

        return committed;
    }

    public void Discard()
    {
        _pending.Clear();
        _markerBeforeAttempt = null;
        _consecutiveFailures = 0;
        _isBlocked = false;
    }

    private async Task<bool> HasAlreadyCommitted(CancellationToken cancellationToken)
    {
        if (_markerBeforeAttempt is null)
        {
            _markerBeforeAttempt = await readMarker(cancellationToken);

            return false;
        }

        return await MarkerAdvanced(cancellationToken);
    }

    private async Task<bool> MarkerAdvanced(CancellationToken cancellationToken)
    {
        var current = await readMarker(cancellationToken);

        return current != _markerBeforeAttempt;
    }

    private IReadOnlyList<SimEvent> TakeCommitted()
    {
        var committed = _pending.ToArray();
        Discard();

        return committed;
    }
}
