using TRPG.Application.Common.Events;
using TRPG.Application.Scenes.Events;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes;

public sealed class ScenePublisher(IGameClientEventSink gameEvents)
{
    public void Publish(Guid worldId, SceneResult scene, WorldStateStamp stamp) =>
        gameEvents.Enqueue(new SceneUpdatedEvent(worldId, scene, stamp));

    public void PublishSeating(SeatingChangedEvent seating) => gameEvents.Enqueue(seating);

    public void PublishCreatureChanges(
        Guid worldId,
        Guid locationId,
        WorldStateStamp stamp,
        IReadOnlySet<Guid> leftIds,
        IReadOnlyCollection<SceneCreatureInfo> arrived,
        IReadOnlyCollection<SceneCreatureInfo> updated
    )
    {
        if (leftIds.Count > 0)
        {
            gameEvents.Enqueue(new CreaturesLeftEvent(worldId, locationId, stamp, leftIds));
        }

        if (arrived.Count > 0)
        {
            gameEvents.Enqueue(new CreaturesArrivedEvent(worldId, locationId, stamp, arrived));
        }

        if (updated.Count > 0)
        {
            gameEvents.Enqueue(new CreaturesUpdatedEvent(worldId, locationId, stamp, updated));
        }
    }

    public void PublishWeather(
        Guid worldId,
        Guid locationId,
        WorldStateStamp stamp,
        WeatherCondition? weather
    ) => gameEvents.Enqueue(new WeatherChangedEvent(worldId, locationId, stamp, weather));

    public void PublishPropOccupancy(
        Guid worldId,
        Guid locationId,
        WorldStateStamp stamp,
        IReadOnlyCollection<PropOccupancy> changes
    ) => gameEvents.Enqueue(new PropOccupancyChangedEvent(worldId, locationId, stamp, changes));

    public void PublishClockReanchored(Guid worldId, Guid locationId, WorldStateStamp stamp) =>
        gameEvents.Enqueue(new ClockReanchoredEvent(worldId, locationId, stamp));
}
