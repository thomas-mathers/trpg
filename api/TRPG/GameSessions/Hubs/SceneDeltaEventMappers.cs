using TRPG.Application.Scenes.Events;
using TRPG.Domain;
using TRPG.GameSessions.Mappers;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class CreaturesArrivedEventMapper : GameClientEventMapper<CreaturesArrivedEvent>
{
    protected override IGameClientCall Map(CreaturesArrivedEvent gameEvent) =>
        new GameClientCall<CreaturesArrivedPayload>(
            new(
                gameEvent.WorldId,
                gameEvent.LocationId,
                gameEvent.Stamp.Version,
                gameEvent.Creatures.Select(x => x.ToStatusSnapshot()).ToArray()
            ),
            static (client, payload) => client.CreaturesArrived(payload)
        );
}

internal sealed class CreaturesLeftEventMapper : GameClientEventMapper<CreaturesLeftEvent>
{
    protected override IGameClientCall Map(CreaturesLeftEvent gameEvent) =>
        new GameClientCall<CreaturesLeftPayload>(
            new(
                gameEvent.WorldId,
                gameEvent.LocationId,
                gameEvent.Stamp.Version,
                gameEvent.CreatureIds.ToArray()
            ),
            static (client, payload) => client.CreaturesLeft(payload)
        );
}

internal sealed class CreaturesMovedEventMapper : GameClientEventMapper<CreaturesMovedEvent>
{
    protected override IGameClientCall Map(CreaturesMovedEvent gameEvent) =>
        new GameClientCall<CreaturesMovedPayload>(
            new(
                gameEvent.WorldId,
                gameEvent.LocationId,
                gameEvent.Stamp.Version,
                gameEvent
                    .CreaturePlacements.Select(x => new CreaturePlacementSnapshot(
                        x.Key,
                        x.Value.ToWire()
                    ))
                    .ToArray()
            ),
            static (client, payload) => client.CreaturesMoved(payload)
        );
}

internal sealed class CreaturesUpdatedEventMapper : GameClientEventMapper<CreaturesUpdatedEvent>
{
    protected override IGameClientCall Map(CreaturesUpdatedEvent gameEvent) =>
        new GameClientCall<CreaturesUpdatedPayload>(
            new(
                gameEvent.WorldId,
                gameEvent.LocationId,
                gameEvent.Stamp.Version,
                gameEvent.Creatures.Select(x => x.ToStatusSnapshot()).ToArray()
            ),
            static (client, payload) => client.CreaturesUpdated(payload)
        );
}

internal sealed class CaravansArrivedEventMapper : GameClientEventMapper<CaravansArrivedEvent>
{
    protected override IGameClientCall Map(CaravansArrivedEvent gameEvent) =>
        new GameClientCall<CaravansArrivedPayload>(
            new(
                gameEvent.WorldId,
                gameEvent.LocationId,
                gameEvent.Stamp.Version,
                gameEvent.Caravans.Select(x => x.ToSnapshot()).ToArray()
            ),
            static (client, payload) => client.CaravansArrived(payload)
        );
}

internal sealed class CaravansLeftEventMapper : GameClientEventMapper<CaravansLeftEvent>
{
    protected override IGameClientCall Map(CaravansLeftEvent gameEvent) =>
        new GameClientCall<CaravansLeftPayload>(
            new(
                gameEvent.WorldId,
                gameEvent.LocationId,
                gameEvent.Stamp.Version,
                gameEvent.CaravanIds.ToArray()
            ),
            static (client, payload) => client.CaravansLeft(payload)
        );
}

internal sealed class CaravansUpdatedEventMapper : GameClientEventMapper<CaravansUpdatedEvent>
{
    protected override IGameClientCall Map(CaravansUpdatedEvent gameEvent) =>
        new GameClientCall<CaravansUpdatedPayload>(
            new(
                gameEvent.WorldId,
                gameEvent.LocationId,
                gameEvent.Stamp.Version,
                gameEvent.Caravans.Select(x => x.ToSnapshot()).ToArray()
            ),
            static (client, payload) => client.CaravansUpdated(payload)
        );
}

internal sealed class WeatherChangedEventMapper : GameClientEventMapper<WeatherChangedEvent>
{
    protected override IGameClientCall Map(WeatherChangedEvent gameEvent) =>
        new GameClientCall<WeatherChangedPayload>(
            new(
                gameEvent.WorldId,
                gameEvent.LocationId,
                gameEvent.Stamp.Version,
                gameEvent.WeatherCondition?.ToResponse()
            ),
            static (client, payload) => client.WeatherChanged(payload)
        );
}

internal sealed class ClockReanchoredEventMapper : GameClientEventMapper<ClockReanchoredEvent>
{
    protected override IGameClientCall Map(ClockReanchoredEvent gameEvent) =>
        new GameClientCall<ClockReanchoredPayload>(
            new(
                gameEvent.WorldId,
                gameEvent.LocationId,
                gameEvent.Stamp.Version,
                (long)(gameEvent.Stamp.GameTime - GameClock.Epoch).TotalMilliseconds,
                gameEvent.Stamp.CapturedAt.ToUnixTimeMilliseconds(),
                gameEvent.Stamp.TimeScale
            ),
            static (client, payload) => client.ClockReanchored(payload)
        );
}
