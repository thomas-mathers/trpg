using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Events;

namespace TRPG.GameSessions.Hubs;

internal sealed class GameClientEventAckGate(
    IGameClientEventDispatcher eventDispatcher,
    IHubContext<ChatHub, IGameClient> hubContext,
    PendingEventAckRegistry pendingEventAcks,
    ILogger<GameClientEventAckGate> logger
) : IGameClientEventAckGate
{
    private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(5);

    public async Task FlushAndAwaitAckAsync(
        Guid worldId,
        CancellationToken cancellationToken = default
    )
    {
        var sentAnything = await eventDispatcher.FlushAsync(worldId, cancellationToken);
        if (!sentAnything)
        {
            return;
        }

        var flushId = Guid.NewGuid();
        var ackTask = pendingEventAcks.Register(flushId);

        var client = hubContext.Clients.Group(GameClientGroups.ForWorld(worldId));
        await client.RequestAck(flushId);

        var timeoutTask = Task.Delay(AckTimeout, cancellationToken);
        var completed = await Task.WhenAny(ackTask, timeoutTask);
        if (completed == timeoutTask)
        {
            logger.LogWarning(
                "Timed out after {TimeoutSeconds}s waiting for client to acknowledge flush {FlushId} in world {WorldId}",
                AckTimeout.TotalSeconds,
                flushId,
                worldId
            );
            pendingEventAcks.Cancel(flushId);
        }
    }
}
