using TRPG.Application.Common.Events;
using TRPG.Application.GameTurns.Events;
using TRPG.Application.GameTurns.Results;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.GameTurns;

internal sealed class PublishedSceneRegistry
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

    public bool RecordIfChanged(Guid playerId, SceneResult scene)
    {
        lock (_gate)
        {
            var previous = _scenes.GetValueOrDefault(playerId);
            if (previous != null && !SceneSemanticComparer.HasPlayerVisibleChange(previous, scene))
            {
                return false;
            }

            _scenes[playerId] = scene;
            return true;
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

internal sealed class ScenePublisher(
    IGameClientEventSink gameEvents,
    PublishedSceneRegistry publishedScenes
)
{
    public void Publish(Guid playerId, SceneResult scene, WorldStateStamp stamp)
    {
        publishedScenes.Record(playerId, scene);
        gameEvents.Enqueue(new SceneUpdatedEvent(scene, stamp));
    }

    public bool PublishIfChanged(Guid playerId, SceneResult scene, WorldStateStamp stamp)
    {
        if (!publishedScenes.RecordIfChanged(playerId, scene))
        {
            return false;
        }

        gameEvents.Enqueue(new SceneUpdatedEvent(scene, stamp));
        return true;
    }
}
