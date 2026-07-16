using System.Collections.Concurrent;

namespace TRPG.Hubs;

internal sealed class WorldConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, string> _connectionIdsByWorldId = new();

    public bool TryAdd(Guid worldId, string connectionId) =>
        _connectionIdsByWorldId.TryAdd(worldId, connectionId);

    public void Remove(Guid worldId, string connectionId) =>
        _connectionIdsByWorldId.TryRemove(new KeyValuePair<Guid, string>(worldId, connectionId));
}
