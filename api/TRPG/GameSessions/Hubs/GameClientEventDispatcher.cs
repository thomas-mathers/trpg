using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Events;

namespace TRPG.GameSessions.Hubs;

internal sealed class GameClientEventDispatcher(
    IGameClientEventBuffer eventBuffer,
    IHubContext<ChatHub, IGameClient> hubContext,
    IEnumerable<IGameClientEventMapper> eventMappers,
    PendingEventAckRegistry pendingEventAcks,
    ILogger<GameClientEventDispatcher> logger
) : IGameClientEventDispatcher
{
    private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(5);

    private readonly IReadOnlyDictionary<Type, IGameClientEventMapper> _eventMappers =
        eventMappers.ToDictionary(mapper => mapper.EventType);

    public async Task FlushAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        await DrainAndSendAsync(worldId, cancellationToken);

    public async Task FlushAndAwaitAckAsync(
        Guid worldId,
        CancellationToken cancellationToken = default
    )
    {
        var sentAnything = await DrainAndSendAsync(worldId, cancellationToken);
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

    private async Task<bool> DrainAndSendAsync(Guid worldId, CancellationToken cancellationToken)
    {
        var pendingEvents = eventBuffer.Drain();
        if (pendingEvents.Count == 0)
        {
            return false;
        }

        var client = hubContext.Clients.Group(GameClientGroups.ForWorld(worldId));
        foreach (var gameEvent in pendingEvents)
        {
            var mapper =
                _eventMappers.GetValueOrDefault(gameEvent.GetType())
                ?? throw new InvalidOperationException(
                    $"No client event mapper is registered for {gameEvent.GetType().Name}."
                );

            logger.LogDebug(
                "Sending client event {EventType} to world {WorldId}",
                gameEvent.GetType().Name,
                worldId
            );

            await mapper.Map(gameEvent).Invoke(client);

            logger.LogDebug(
                "Sent client event {EventType} to world {WorldId}",
                gameEvent.GetType().Name,
                worldId
            );
        }

        return true;
    }
}
