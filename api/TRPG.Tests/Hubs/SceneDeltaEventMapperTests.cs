using Microsoft.AspNetCore.SignalR.Client;
using TRPG.Application.Scenes.Events;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.GameSessions.Hubs;
using TRPG.GameSessions.Responses;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Hubs;

public sealed class SceneDeltaEventMapperTests
{
    [Fact]
    public async Task CreaturesArrived_MapsStatusPlacementAndVersion()
    {
        var worldId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var creature = SceneResultBuilder.MakeCreature("Visitor");
        var stamp = new WorldStateStamp(42, GameClock.Epoch, DateTimeOffset.UnixEpoch, 1);
        CreaturesArrivedPayload? received = null;
        var client = new TestGameClient
        {
            Connection = new HubConnectionBuilder().WithUrl("http://localhost").Build(),
            OnCreaturesArrived = payload => received = payload,
        };

        await new CreaturesArrivedEventMapper()
            .Map(
                new CreaturesArrivedEvent(
                    worldId,
                    locationId,
                    stamp,
                    [creature with { Placement = new Placement(3, 4, 0.5) }]
                )
            )
            .Invoke(client);

        Assert.NotNull(received);
        Assert.Equal(locationId, received.LocationId);
        Assert.Equal(42, received.Version);
        Assert.Equal(new PlacementWire(3, 4, 0.5), Assert.Single(received.Creatures).Placement);
    }

    [Fact]
    public async Task ClockReanchored_MapsTheNewClockAnchor()
    {
        var worldId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var capturedAt = DateTimeOffset.FromUnixTimeMilliseconds(1000);
        var stamp = new WorldStateStamp(7, GameClock.Epoch + TimeSpan.FromHours(2), capturedAt, 2);
        ClockReanchoredPayload? received = null;
        var client = new TestGameClient
        {
            Connection = new HubConnectionBuilder().WithUrl("http://localhost").Build(),
            OnClockReanchored = payload => received = payload,
        };

        await new ClockReanchoredEventMapper()
            .Map(new ClockReanchoredEvent(worldId, locationId, stamp))
            .Invoke(client);

        Assert.NotNull(received);
        Assert.Equal(7, received.Version);
        Assert.Equal((long)TimeSpan.FromHours(2).TotalMilliseconds, received.GameTimeMilliseconds);
        Assert.Equal(1000, received.AnchoredAtUnixMilliseconds);
        Assert.Equal(2, received.TimeScale);
    }

    [Fact]
    public async Task SeatingChanged_MapsSeatPostureAndPlacement()
    {
        var worldId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var creatureId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var stamp = new WorldStateStamp(9, GameClock.Epoch, DateTimeOffset.UnixEpoch, 1);
        SeatingChangedPayload? received = null;
        var client = new TestGameClient
        {
            Connection = new HubConnectionBuilder().WithUrl("http://localhost").Build(),
            OnSeatingChanged = payload => received = payload,
        };

        await new SeatingChangedEventMapper()
            .Map(
                new SeatingChangedEvent(
                    worldId,
                    locationId,
                    stamp,
                    creatureId,
                    seatId,
                    global::TRPG.Domain.Models.CreaturePosture.Sitting,
                    new Placement(3, 4, 0.5)
                )
            )
            .Invoke(client);

        Assert.NotNull(received);
        Assert.Equal(9, received.Version);
        Assert.Equal(creatureId, received.CreatureId);
        Assert.Equal(seatId, received.SeatId);
        Assert.Equal(TRPG.GameSessions.Responses.CreaturePosture.Sitting, received.Posture);
        Assert.Equal(new PlacementWire(3, 4, 0.5), received.Placement);
    }

    [Fact]
    public async Task PropOccupancyChanged_MapsEachPropsOccupancyAndVersion()
    {
        var worldId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var propId = Guid.NewGuid();
        var stamp = new WorldStateStamp(11, GameClock.Epoch, DateTimeOffset.UnixEpoch, 1);
        PropOccupancyChangedPayload? received = null;
        var client = new TestGameClient
        {
            Connection = new HubConnectionBuilder().WithUrl("http://localhost").Build(),
            OnPropOccupancyChanged = payload => received = payload,
        };

        await new PropOccupancyChangedEventMapper()
            .Map(
                new PropOccupancyChangedEvent(
                    worldId,
                    locationId,
                    stamp,
                    [new PropOccupancy(propId, true, false)]
                )
            )
            .Invoke(client);

        Assert.NotNull(received);
        Assert.Equal(11, received.Version);
        Assert.Equal(new PropOccupancySnapshot(propId, true, false), Assert.Single(received.Props));
    }
}
