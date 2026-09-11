using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Events;
using TRPG.Application.Configuration;

namespace TRPG.GameSessions.Hubs;

internal sealed class GameClientEventAckGate(
    IGameClientEventDispatcher eventDispatcher,
    IHubContext<ChatHub, IGameClient> hubContext,
    PendingEventAckRegistry pendingEventAcks,
    IOptionsSnapshot<GameClientEventAckOptions> optionsSnapshot,
    ILogger<GameClientEventAckGate> logger
) : IGameClientEventAckGate
{
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

        var ackTimeout = optionsSnapshot.Value.AckTimeout;
        var timeoutTask = Task.Delay(ackTimeout, cancellationToken);
        var completed = await Task.WhenAny(ackTask, timeoutTask);
        if (completed == timeoutTask)
        {
            logger.LogWarning(
                "Timed out after {TimeoutSeconds}s waiting for client to acknowledge flush {FlushId} in world {WorldId}",
                ackTimeout.TotalSeconds,
                flushId,
                worldId
            );
            pendingEventAcks.Cancel(flushId);
        }
    }
}
