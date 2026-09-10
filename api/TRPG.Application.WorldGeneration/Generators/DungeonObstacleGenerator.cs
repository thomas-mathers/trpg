using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal enum DungeonObstacleKind
{
    KeyLock,
    Miniboss,
    TrapGauntlet,
}

internal record DungeonObstacleInput(
    IReadOnlyList<DungeonRoomPlacement> Placements,
    IReadOnlyList<LocationConnector> LocationConnectors,
    Guid EntranceLocationId,
    Guid BuildingId,
    BuildingType DungeonType,
    Guid WorldId,
    Guid StateId,
    IReadOnlyDictionary<CreatureType, Faction> FactionsByCreatureType,
    Random Random
);

internal record DungeonObstacleResult(
    IReadOnlyList<DoorConnector> DoorConnectors,
    IReadOnlyList<Item> Items,
    IReadOnlyList<DoorConnectorKey> DoorConnectorKeys,
    IReadOnlyList<Room> Rooms,
    IReadOnlyList<Location> Locations,
    IReadOnlyList<LocationConnector> LocationConnectors,
    IReadOnlyList<CreatureGeneratorResult> Monsters,
    IReadOnlyList<CreatureJob> Jobs,
    IReadOnlyList<EncounterGroup> EncounterGroups,
    IReadOnlyList<EncounterGroupMember> EncounterGroupMembers,
    IReadOnlyList<CreatureSpawner> CreatureSpawners,
    IReadOnlyList<Trigger> Triggers
)
{
    public static readonly DungeonObstacleResult Empty = new(
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        []
    );
}

// The Short (and rarely Third) route always carries an obstacle, drawn from the same pool
// KeyLock/Miniboss/TrapGauntlet regardless of which route it lands on. KeyLock and Miniboss both
// force a single occupant into a room rather than leaving it to the normal random population
// roll — the whole point of an obstacle is that the room is never empty.
public class DungeonObstacleGenerator(DungeonPopulator dungeonPopulator)
{
    // A forced occupant is generated at this elevated baseline level rather than the normal
    // world-gen baseline of 1, so a Miniboss room reads as meaningfully harder than an ordinary
    // occupant without needing new level-scaling machinery — CreatureSpawnFiller's existing
    // spawn-level curve does the rest.
    private const int MinibossPlayerLevelBonus = 4;

    internal DungeonObstacleResult Generate(DungeonObstacleInput input, DungeonObstacleKind kind)
    {
        var shortRouteRooms = RouteRooms(input.Placements, DungeonRouteKind.Short);
        if (shortRouteRooms.Count == 0)
        {
            return DungeonObstacleResult.Empty;
        }

        return kind switch
        {
            DungeonObstacleKind.KeyLock => BuildKeyLock(input, shortRouteRooms),
            DungeonObstacleKind.Miniboss => BuildMiniboss(input, shortRouteRooms),
            DungeonObstacleKind.TrapGauntlet => BuildTrapGauntlet(input, shortRouteRooms),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private static readonly TrapKind[] GauntletTrapKinds =
    [
        TrapKind.Mechanical,
        TrapKind.Collapse,
        TrapKind.Slope,
        TrapKind.Water,
    ];

    // Every room on the gated route is a deliberate trap, not the ambient system's rare
    // room-eligibility roll — this is authored, one kind per room, cycling through all four so a
    // longer gauntlet mixes kinds instead of repeating one. Climbability reuses the exact same
    // DungeonTrapBasement the ambient system uses, so a fall behaves identically either way.
    private static DungeonObstacleResult BuildTrapGauntlet(
        DungeonObstacleInput input,
        IReadOnlyList<DungeonRoomPlacement> shortRouteRooms
    )
    {
        var orderedRooms = shortRouteRooms.OrderBy(room => room.DepthFromEntrance).ToArray();
        var gauntletLocationIds = orderedRooms.Select(room => room.Room.LocationId).ToHashSet();
        var basement = new DungeonTrapBasement(input.WorldId, input.StateId, input.BuildingId);

        var triggers = new List<Trigger>();
        for (var step = 0; step < orderedRooms.Length; step++)
        {
            var room = orderedRooms[step];
            var kind = GauntletTrapKinds[step % GauntletTrapKinds.Length];
            var targetId =
                kind == TrapKind.Water
                    ? ChooseWaterTarget(room, input.Placements, gauntletLocationIds)
                    : basement.TargetFor(kind, room);

            if (targetId == null)
            {
                continue;
            }

            triggers.Add(
                new Trigger
                {
                    LocationId = room.Room.LocationId,
                    WorldId = input.WorldId,
                    Name = TrapName(kind),
                    Description = TrapDescription(kind),
                    TrapKind = kind,
                    TargetId = targetId,
                }
            );
        }

        return DungeonObstacleResult.Empty with
        {
            Rooms = basement.Rooms,
            Locations = basement.Locations,
            LocationConnectors = basement.LocationConnectors,
            Triggers = triggers,
        };
    }

    // Water is the only mechanism that plausibly moves you backwards, sweeping you shallower on
    // the same floor rather than down. A dungeon layout that offers no candidate in that direction
    // simply gets no trap here.
    private static Guid? ChooseWaterTarget(
        DungeonRoomPlacement placement,
        IReadOnlyList<DungeonRoomPlacement> placements,
        IReadOnlySet<Guid> gauntletLocationIds
    )
    {
        var candidates = placements
            .Where(other => other.Room.LocationId != placement.Room.LocationId)
            .Where(other => !gauntletLocationIds.Contains(other.Room.LocationId))
            .Where(other => other.FloorNumber == placement.FloorNumber)
            .Where(other => other.DepthFromEntrance < placement.DepthFromEntrance)
            .ToArray();

        return candidates.Length == 0 ? null : candidates[0].Room.LocationId;
    }

    private static string TrapName(TrapKind kind) =>
        kind switch
        {
            TrapKind.Mechanical => "Trapdoor",
            TrapKind.Collapse => "Weak Floor",
            TrapKind.Slope => "Loose Footing",
            TrapKind.Water => "Sudden Current",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    private static string TrapDescription(TrapKind kind) =>
        kind switch
        {
            TrapKind.Mechanical => "A catch waits to drop the floor out from under you.",
            TrapKind.Collapse => "The floor here will not hold much longer.",
            TrapKind.Slope => "Loose scree and old ice make for treacherous footing.",
            TrapKind.Water => "A current strong enough to carry you off your feet.",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    private DungeonObstacleResult BuildKeyLock(
        DungeonObstacleInput input,
        IReadOnlyList<DungeonRoomPlacement> shortRouteRooms
    )
    {
        var entryRoom = RouteRoom(shortRouteRooms, depth: 1);
        var entryConnector = input.LocationConnectors.Single(connector =>
            connector.OriginLocationId == input.EntranceLocationId
            && connector.DestinationLocationId == entryRoom.Room.LocationId
        );
        var door = new DoorConnector
        {
            ConnectorId = entryConnector.Id,
            IsLocked = true,
            WorldId = input.WorldId,
        };

        var longRouteFirstRoom = RouteRoom(
            RouteRooms(input.Placements, DungeonRouteKind.Long),
            depth: 1
        );
        var spur = BuildKeySpur(input, longRouteFirstRoom);
        var keyRoomLocation = spur.Locations[^1];

        var guard = dungeonPopulator.GenerateForced(
            input.WorldId,
            keyRoomLocation.Id,
            input.DungeonType,
            playerLevel: 1,
            input.FactionsByCreatureType
        );
        var guardCreature = guard.Monsters.Single().Creature;

        var key = new Key
        {
            WorldId = input.WorldId,
            Name = $"Key to {entryRoom.Room.Name}",
            Description = $"A key that unlocks the way into {entryRoom.Room.Name}.",
            Quantity = 1,
            Ownership = new ItemOwnership
            {
                OwnerId = guardCreature.Id,
                OwnerType = OwnerType.Creature,
            },
        };
        var doorConnectorKey = new DoorConnectorKey
        {
            ItemId = key.Id,
            DoorConnectorId = door.Id,
            WorldId = input.WorldId,
        };

        return new DungeonObstacleResult(
            [door],
            [key],
            [doorConnectorKey],
            spur.Rooms,
            spur.Locations,
            spur.Connectors,
            guard.Monsters,
            guard.Jobs,
            guard.EncounterGroups,
            guard.EncounterGroupMembers,
            [guard.Spawner],
            []
        );
    }

    private sealed record DungeonSpurResult(
        IReadOnlyList<Room> Rooms,
        IReadOnlyList<Location> Locations,
        IReadOnlyList<LocationConnector> Connectors
    );

    // A guarded two-hop spur, not a single adjacent room — the key only makes the short route a
    // real choice if finding it costs a real detour, and it needs a fight to be worth locking a
    // route behind in the first place.
    private static DungeonSpurResult BuildKeySpur(
        DungeonObstacleInput input,
        DungeonRoomPlacement branchFrom
    )
    {
        var sidePassage = NewRoom(
            input,
            "Side Passage",
            "A narrow passage branching off, unremarkable at a glance."
        );
        var keyRoom = NewRoom(
            input,
            "Guarded Alcove",
            "Someone has made a home of this alcove, and isn't eager for company."
        );

        var connectors = new List<LocationConnector>();
        connectors.AddRange(
            TwoWayConnector(
                input.WorldId,
                branchFrom.Room.LocationId,
                branchFrom.Room.Name,
                sidePassage.Location
            )
        );
        connectors.AddRange(
            TwoWayConnector(
                input.WorldId,
                sidePassage.Location.Id,
                sidePassage.Location.Name,
                keyRoom.Location
            )
        );

        return new DungeonSpurResult(
            [sidePassage.Room, keyRoom.Room],
            [sidePassage.Location, keyRoom.Location],
            connectors
        );
    }

    private DungeonObstacleResult BuildMiniboss(
        DungeonObstacleInput input,
        IReadOnlyList<DungeonRoomPlacement> shortRouteRooms
    )
    {
        var lastRoom = shortRouteRooms.OrderByDescending(room => room.DepthFromEntrance).First();

        var miniboss = dungeonPopulator.GenerateForced(
            input.WorldId,
            lastRoom.Room.LocationId,
            input.DungeonType,
            playerLevel: 1 + MinibossPlayerLevelBonus,
            input.FactionsByCreatureType
        );

        return DungeonObstacleResult.Empty with
        {
            Monsters = miniboss.Monsters,
            Jobs = miniboss.Jobs,
            EncounterGroups = miniboss.EncounterGroups,
            EncounterGroupMembers = miniboss.EncounterGroupMembers,
            CreatureSpawners = [miniboss.Spawner],
        };
    }

    private static IReadOnlyList<DungeonRoomPlacement> RouteRooms(
        IReadOnlyList<DungeonRoomPlacement> placements,
        DungeonRouteKind kind
    ) =>
        placements
            .Where(placement => placement.RouteKind == kind && !placement.IsDeadEnd)
            .ToArray();

    private static DungeonRoomPlacement RouteRoom(
        IReadOnlyList<DungeonRoomPlacement> routeRooms,
        int depth
    ) => routeRooms.Single(placement => placement.DepthFromEntrance == depth);

    private sealed record NewRoomResult(Room Room, Location Location);

    private static NewRoomResult NewRoom(
        DungeonObstacleInput input,
        string name,
        string description
    )
    {
        var roomId = Guid.NewGuid();
        var location = LocationGenerator.Generate(
            input.WorldId,
            input.StateId,
            roomId: roomId,
            name: name
        );
        var room = new Room
        {
            Id = roomId,
            BuildingId = input.BuildingId,
            LocationId = location.Id,
            Name = name,
            Description = description,
            WorldId = input.WorldId,
        };

        return new NewRoomResult(room, location);
    }

    private static IEnumerable<LocationConnector> TwoWayConnector(
        Guid worldId,
        Guid fromLocationId,
        string fromName,
        Location to
    )
    {
        yield return new LocationConnector
        {
            OriginLocationId = fromLocationId,
            Name = "Passage",
            Description = $"The way to {to.Name}.",
            DestinationLocationId = to.Id,
            DestinationLabel = to.Name,
            WorldId = worldId,
        };
        yield return new LocationConnector
        {
            OriginLocationId = to.Id,
            Name = "Passage",
            Description = "The way back.",
            DestinationLocationId = fromLocationId,
            DestinationLabel = fromName,
            WorldId = worldId,
        };
    }
}
