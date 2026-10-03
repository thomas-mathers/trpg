using TRPG.Application.Common.Events;
using TRPG.Application.Scenes.Events;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.Scenes;

public sealed class PublishedSceneRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, SceneResult> _scenes = [];

    public void Record(Guid playerId, SceneResult scene)
    {
        lock (_gate)
        {
            _scenes[playerId] = scene;
        }
    }

    public SceneResult? Find(Guid playerId)
    {
        lock (_gate)
        {
            return _scenes.GetValueOrDefault(playerId);
        }
    }
}

public sealed class ScenePublisher(
    IGameClientEventSink gameEvents,
    PublishedSceneRegistry publishedScenes
)
{
    public void Publish(Guid worldId, Guid playerId, SceneResult scene, WorldStateStamp stamp)
    {
        publishedScenes.Record(playerId, scene);
        gameEvents.Enqueue(new SceneUpdatedEvent(worldId, scene, stamp));
    }

    public bool PublishIfChanged(
        Guid worldId,
        Guid playerId,
        SceneResult scene,
        WorldStateStamp stamp
    )
    {
        var previous = publishedScenes.Find(playerId);
        return PublishPlan(playerId, scene, stamp, SceneChangePlanner.Plan(previous, scene, stamp));
    }

    public bool PublishAfterTimeAdvance(Guid playerId, SceneResult scene, WorldStateStamp stamp)
    {
        var previous = publishedScenes.Find(playerId);
        return PublishPlan(
            playerId,
            scene,
            stamp,
            SceneChangePlanner.PlanAfterTimeAdvance(previous, scene, stamp)
        );
    }

    private bool PublishPlan(
        Guid playerId,
        SceneResult scene,
        WorldStateStamp stamp,
        SceneChangePlan plan
    )
    {
        publishedScenes.Record(playerId, scene);
        if (plan.RequiresSnapshot)
        {
            gameEvents.Enqueue(new SceneUpdatedEvent(scene.WorldId, scene, stamp));
            return true;
        }

        foreach (var gameEvent in plan.Events)
        {
            gameEvents.Enqueue(gameEvent);
        }

        return plan.Events.Count > 0;
    }
}
