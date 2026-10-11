using TRPG.Application.Common.Events;
using TRPG.Application.Scenes.Events;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain.Models;

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

    public void PublishSeating(Guid playerId, SeatingChangedEvent seating)
    {
        var previous = publishedScenes.Find(playerId);
        if (previous is not null)
        {
            publishedScenes.Record(playerId, SeatingProjection.Apply(previous, seating));
        }

        gameEvents.Enqueue(seating);
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

    public bool PublishCreatureChanges(Guid playerId, SceneResult current, WorldStateStamp stamp)
    {
        var previous = publishedScenes.Find(playerId);
        if (previous is null)
        {
            return false;
        }

        publishedScenes.Record(playerId, current);
        var events = SceneChangePlanner.PlanCreatureChanges(previous, current, stamp);
        foreach (var gameEvent in events)
        {
            gameEvents.Enqueue(gameEvent);
        }

        return events.Count > 0;
    }

    public bool PublishWeather(Guid playerId, WeatherCondition? weather, WorldStateStamp stamp)
    {
        var previous = publishedScenes.Find(playerId);
        if (previous is null || previous.Weather == weather)
        {
            return false;
        }

        publishedScenes.Record(playerId, previous with { Weather = weather });
        gameEvents.Enqueue(
            new WeatherChangedEvent(previous.WorldId, previous.LocationId, stamp, weather)
        );
        return true;
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
