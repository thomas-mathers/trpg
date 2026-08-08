using Microsoft.AspNetCore.SignalR;

namespace TRPG.GameSessions.Hubs;

internal sealed class GameClientEventDispatcher(
    IGameClientEventBuffer eventBuffer,
    IHubContext<ChatHub> hubContext
)
{
    public async Task FlushAsync(Guid worldId, CancellationToken cancellationToken = default)
    {
        var clients = hubContext.Clients.Group(GameClientGroups.ForWorld(worldId));
        foreach (var gameEvent in eventBuffer.Drain())
        {
            if (gameEvent.Payload == null)
            {
                await clients.SendAsync(gameEvent.MethodName, cancellationToken);
            }
            else
            {
                await clients.SendAsync(gameEvent.MethodName, gameEvent.Payload, cancellationToken);
            }
        }
    }
}
