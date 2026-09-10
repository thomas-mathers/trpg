using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record DungeonLeverGeneratorResult(
    IReadOnlyList<Lever> Levers,
    IReadOnlyList<DoorConnector> DoorConnectors,
    IReadOnlyList<DoorConnectorLever> DoorConnectorLevers
)
{
    public static readonly DungeonLeverGeneratorResult Empty = new([], [], []);
}

// A lever is a remote state change rather than a portable item, so unlike a key it can live on a
// different route than the gate it opens. Every dungeon gets the mandatory boss-to-entrance
// shortcut lever; the canonical multi-lever gate is layered on only when the layout rolled one.
internal static class DungeonLeverGenerator
{
    private static readonly DungeonRouteKind[] GateRouteKinds =
    [
        DungeonRouteKind.Long,
        DungeonRouteKind.Short,
        DungeonRouteKind.Third,
    ];

    // Free to walk from the boss side, locked from the entrance side until pulled — the lever
    // itself sits in the boss room, so it can only ever be found after the boss is dealt with.
    public static DungeonLeverGeneratorResult BuildMandatoryShortcutLever(
        Guid worldId,
        Guid bossLocationId,
        Guid entranceLocationId,
        Guid backDoorLocationId,
        IReadOnlyCollection<LocationConnector> locationConnectors
    )
    {
        var entryConnector = locationConnectors.Single(connector =>
            connector.OriginLocationId == entranceLocationId
            && connector.DestinationLocationId == backDoorLocationId
        );

        var door = new DoorConnector
        {
            ConnectorId = entryConnector.Id,
            IsLocked = true,
            WorldId = worldId,
        };
        var lever = new Lever
        {
            WorldId = worldId,
            Name = "Lever",
            Description = "A lever built into the wall, streaked with rust.",
            LocationId = bossLocationId,
        };

        return new DungeonLeverGeneratorResult(
            [lever],
            [door],
            [LinkLever(worldId, lever.Id, door.Id)]
        );
    }

    // Every route present gets its own lever, all gating the single connector from the shared
    // convergence room into the boss — mandatory precisely because it demands having explored more
    // than one route, and each lever sits at its route's last room so a route's own obstacle is
    // always cleared before its lever can be found.
    public static DungeonLeverGeneratorResult BuildCanonicalGate(
        Guid worldId,
        Guid landingLocationId,
        Guid bossLocationId,
        IReadOnlyCollection<LocationConnector> locationConnectors,
        IReadOnlyList<DungeonRoomPlacement> placements
    )
    {
        var gatedConnector = locationConnectors.Single(connector =>
            connector.OriginLocationId == landingLocationId
            && connector.DestinationLocationId == bossLocationId
        );
        var door = new DoorConnector
        {
            ConnectorId = gatedConnector.Id,
            IsLocked = true,
            WorldId = worldId,
        };

        var levers = new List<Lever>();
        var doorConnectorLevers = new List<DoorConnectorLever>();
        foreach (var kind in GateRouteKinds)
        {
            var routeRooms = placements
                .Where(placement => placement.RouteKind == kind && !placement.IsDeadEnd)
                .ToArray();
            if (routeRooms.Length == 0)
            {
                continue;
            }

            var lastRoom = routeRooms.OrderByDescending(room => room.DepthFromEntrance).First();
            var lever = new Lever
            {
                WorldId = worldId,
                Name = "Lever",
                Description = "A lever set into an alcove, worn smooth by use.",
                LocationId = lastRoom.Room.LocationId,
            };
            levers.Add(lever);
            doorConnectorLevers.Add(LinkLever(worldId, lever.Id, door.Id));
        }

        return new DungeonLeverGeneratorResult(levers, [door], doorConnectorLevers);
    }

    private static DoorConnectorLever LinkLever(Guid worldId, Guid leverId, Guid doorConnectorId) =>
        new()
        {
            LeverId = leverId,
            DoorConnectorId = doorConnectorId,
            WorldId = worldId,
        };
}
