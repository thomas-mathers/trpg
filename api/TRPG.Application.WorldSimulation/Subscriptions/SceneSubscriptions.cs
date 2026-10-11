using Microsoft.Extensions.Options;

namespace TRPG.Application.WorldSimulation.Subscriptions;

public enum ResyncState
{
    Synced,
    Pending,
    Sent,
    Exhausted,
}

public sealed class SceneSubscriptions(
    IOptions<WorldSimulationOptions> options,
    TimeProvider timeProvider
)
{
    private readonly object _gate = new();
    private readonly Dictionary<WatchKey, Subscription> _watches = [];

    public void Watch(Guid worldId, Guid playerId, Guid locationId)
    {
        lock (_gate)
        {
            var key = new WatchKey(worldId, playerId);
            if (_watches.TryGetValue(key, out var watch))
            {
                watch.LocationId = locationId;
                return;
            }

            _watches[key] = new Subscription(locationId);
        }
    }

    public bool IsWatchingLocation(Guid worldId, Guid locationId)
    {
        lock (_gate)
        {
            return _watches.Any(entry =>
                entry.Key.WorldId == worldId && entry.Value.LocationId == locationId
            );
        }
    }

    public ResyncState? StateOf(Guid worldId, Guid playerId)
    {
        lock (_gate)
        {
            return _watches.TryGetValue(new WatchKey(worldId, playerId), out var watch)
                ? watch.State
                : null;
        }
    }

    public void MarkPlayerForResync(Guid worldId, Guid playerId)
    {
        lock (_gate)
        {
            if (_watches.TryGetValue(new WatchKey(worldId, playerId), out var watch))
            {
                RecordFailure(watch);
            }
        }
    }

    public void MarkLocationForResync(Guid worldId, Guid locationId)
    {
        lock (_gate)
        {
            foreach (var (key, watch) in _watches)
            {
                if (key.WorldId == worldId && watch.LocationId == locationId)
                {
                    RecordFailure(watch);
                }
            }
        }
    }

    public void MarkWorldForResync(Guid worldId)
    {
        lock (_gate)
        {
            foreach (var (key, watch) in _watches)
            {
                if (key.WorldId == worldId && watch.State == ResyncState.Synced)
                {
                    RecordFailure(watch);
                }
            }
        }
    }

    public IReadOnlyCollection<Guid> TakePending(Guid worldId)
    {
        lock (_gate)
        {
            var pending = _watches
                .Where(entry =>
                    entry.Key.WorldId == worldId && entry.Value.State == ResyncState.Pending
                )
                .ToArray();
            foreach (var (_, watch) in pending)
            {
                watch.State = ResyncState.Sent;
            }

            return pending.Select(entry => entry.Key.PlayerId).ToArray();
        }
    }

    public void CompleteResync(Guid worldId, Guid playerId)
    {
        lock (_gate)
        {
            if (
                _watches.TryGetValue(new WatchKey(worldId, playerId), out var watch)
                && watch.State == ResyncState.Sent
            )
            {
                watch.State = ResyncState.Synced;
                watch.Attempts = 0;
            }
        }
    }

    public IReadOnlyCollection<Guid> TakeExhausted(Guid worldId)
    {
        lock (_gate)
        {
            var exhausted = _watches
                .Where(entry =>
                    entry.Key.WorldId == worldId && entry.Value.State == ResyncState.Exhausted
                )
                .Select(entry => entry.Key)
                .ToArray();
            foreach (var key in exhausted)
            {
                _watches.Remove(key);
            }

            return exhausted.Select(key => key.PlayerId).ToArray();
        }
    }

    private void RecordFailure(Subscription watch)
    {
        var now = timeProvider.GetUtcNow();
        if (watch.State == ResyncState.Synced)
        {
            watch.State = ResyncState.Pending;
            watch.Attempts = 0;
            watch.WindowStartedAt = now;
            return;
        }

        if (watch.State == ResyncState.Exhausted)
        {
            return;
        }

        if (now - watch.WindowStartedAt > options.Value.ResyncWindow)
        {
            watch.Attempts = 0;
            watch.WindowStartedAt = now;
        }

        watch.Attempts++;
        watch.State =
            watch.Attempts >= options.Value.ResyncAttempts
                ? ResyncState.Exhausted
                : ResyncState.Pending;
    }

    private readonly record struct WatchKey(Guid WorldId, Guid PlayerId);

    private sealed class Subscription(Guid locationId)
    {
        public Guid LocationId { get; set; } = locationId;
        public ResyncState State { get; set; }
        public int Attempts { get; set; }
        public DateTimeOffset WindowStartedAt { get; set; }
    }
}
