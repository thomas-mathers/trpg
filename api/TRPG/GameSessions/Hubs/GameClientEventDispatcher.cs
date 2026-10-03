using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Events;

namespace TRPG.GameSessions.Hubs;

internal sealed class GameClientEventDispatcher(
    IGameClientEventDrain gameClientEventDrain,
    IHubContext<ChatHub, IGameClient> hubContext,
    IEnumerable<IGameClientEventMapper> eventMappers,
    ILogger<GameClientEventDispatcher> logger
) : IGameClientEventDispatcher
{
    private readonly IReadOnlyDictionary<Type, IGameClientEventMapper> _eventMappers =
        eventMappers.ToDictionary(mapper => mapper.EventType);

    public async Task<bool> FlushAsync(Guid worldId, CancellationToken cancellationToken = default)
    {
        var sendGate = gameClientEventDrain.GetSendGate(worldId);
        await sendGate.WaitAsync(cancellationToken);
        try
        {
            return await SendPendingAsync(worldId);
        }
        finally
        {
            sendGate.Release();
        }
    }

    private async Task<bool> SendPendingAsync(Guid worldId)
    {
        var pendingEvents = gameClientEventDrain.Drain(worldId);

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
