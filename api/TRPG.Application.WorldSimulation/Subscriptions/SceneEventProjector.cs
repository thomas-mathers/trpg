using TRPG.Application.Common.Events;
using TRPG.Application.Scenes.Events;

namespace TRPG.Application.WorldSimulation.Subscriptions;

public sealed class SceneEventProjector(SceneSubscriptions subscriptions)
{
    public IReadOnlyList<GameClientEvent> Select(
        Guid worldId,
        IReadOnlyList<GameClientEvent> events
    )
    {
        var selected = new List<GameClientEvent>(events.Count);
        foreach (var gameEvent in events)
        {
            if (IsSubscribed(worldId, gameEvent))
            {
                selected.Add(gameEvent);
            }
        }

        return selected;
    }

    public bool IsSceneEvent(GameClientEvent gameEvent) =>
        gameEvent is SceneUpdatedEvent or ILocationScopedEvent;

    public void RecordSendFailure(GameClientEvent gameEvent)
    {
        switch (gameEvent)
        {
            case SceneUpdatedEvent snapshot:
                subscriptions.MarkPlayerForResync(snapshot.WorldId, snapshot.Scene.Player.Id);
                break;
            case ILocationScopedEvent delta:
                subscriptions.MarkLocationForResync(gameEvent.WorldId, delta.LocationId);
                break;
        }
    }

    private bool IsSubscribed(Guid worldId, GameClientEvent gameEvent)
    {
        switch (gameEvent)
        {
            case SceneUpdatedEvent snapshot:
                subscriptions.Watch(worldId, snapshot.Scene.Player.Id, snapshot.Scene.LocationId);
                return true;
            case ILocationScopedEvent delta:
                return subscriptions.IsWatchingLocation(worldId, delta.LocationId);
            default:
                return true;
        }
    }
}
