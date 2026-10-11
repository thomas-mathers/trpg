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

    public bool PublishPropOccupancy(
        Guid playerId,
        IReadOnlyCollection<PropOccupancy> changes,
        WorldStateStamp stamp
    )
    {
        var previous = publishedScenes.Find(playerId);
        if (previous is null || changes.Count == 0)
        {
            return false;
        }

        var change = new PropOccupancyChangedEvent(
            previous.WorldId,
            previous.LocationId,
            stamp,
            changes
        );
        publishedScenes.Record(playerId, PropOccupancyProjection.Apply(previous, change));
        gameEvents.Enqueue(change);
        return true;
    }

    public void PublishClockReanchored(Guid worldId, Guid locationId, WorldStateStamp stamp) =>
        gameEvents.Enqueue(new ClockReanchoredEvent(worldId, locationId, stamp));
}
