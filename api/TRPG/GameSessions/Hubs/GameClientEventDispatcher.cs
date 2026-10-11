using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Events;
using TRPG.Application.WorldSimulation.Subscriptions;

namespace TRPG.GameSessions.Hubs;

internal sealed class GameClientEventDispatcher(
    IGameClientEventDrain gameClientEventDrain,
    IHubContext<ChatHub, IGameClient> hubContext,
    IEnumerable<IGameClientEventMapper> eventMappers,
    SceneEventProjector sceneEventProjector,
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
        var drainedEvents = gameClientEventDrain.Drain(worldId);
        var pendingEvents = sceneEventProjector.Select(worldId, drainedEvents);

        if (pendingEvents.Count == 0)
        {
            return false;
        }

        var client = hubContext.Clients.Group(GameClientGroups.ForWorld(worldId));

        foreach (var gameEvent in pendingEvents)
        {
            await SendIsolated(worldId, client, gameEvent);
        }

        return true;
    }

    private async Task SendIsolated(Guid worldId, IGameClient client, GameClientEvent gameEvent)
    {
        try
        {
            await Send(worldId, client, gameEvent);
        }
        catch (Exception exception) when (sceneEventProjector.IsSceneEvent(gameEvent))
        {
            logger.LogError(
                exception,
                "Sending scene event {EventType} to world {WorldId} failed; resyncing the scene",
                gameEvent.GetType().Name,
                worldId
            );
            sceneEventProjector.RecordSendFailure(gameEvent);
        }
    }

    private async Task Send(Guid worldId, IGameClient client, GameClientEvent gameEvent)
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
}
