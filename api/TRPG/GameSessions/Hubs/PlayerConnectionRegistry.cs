using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using TRPG.Application.WorldSimulation.Subscriptions;

namespace TRPG.GameSessions.Hubs;

internal sealed class PlayerConnectionRegistry : IPlayerConnectionCloser
{
    private readonly ConcurrentDictionary<Guid, HubCallerContext> _connections = new();

    public void Register(Guid playerId, HubCallerContext context) =>
        _connections[playerId] = context;

    public void Unregister(Guid playerId, HubCallerContext context) =>
        _connections.TryRemove(new KeyValuePair<Guid, HubCallerContext>(playerId, context));

    public void Close(Guid playerId)
    {
        if (_connections.TryGetValue(playerId, out var context))
        {
            context.Abort();
        }
    }
}
