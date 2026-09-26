using System.Collections.Concurrent;
using TRPG.Application.Common.Events;
using TRPG.Application.GameTurns.Events;
using TRPG.Application.GameTurns.Results;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.GameTurns;

internal sealed class PublishedSceneRegistry
{
    private readonly ConcurrentDictionary<Guid, SceneResult> _scenes = new();

    public void Record(Guid playerId, SceneResult scene) => _scenes[playerId] = scene;

    public SceneResult? Find(Guid playerId) => _scenes.GetValueOrDefault(playerId);
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
}
