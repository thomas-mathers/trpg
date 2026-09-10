using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record DungeonTrapResult(
    IReadOnlyList<Trigger> Triggers,
    IReadOnlyList<Room> Rooms,
    IReadOnlyList<Location> Locations,
    IReadOnlyList<LocationConnector> LocationConnectors
);

// A trap's kind is implied entirely by its room's role, so there is no separate kind roll: a room
// already reads as a hazard (CollapsedGallery, FloodedSump) or a place worth guarding
// (TreasureRoom), and the trap makes that mechanical rather than only decorative. Falling-trap
// climbability (Rubble Landing / Trap Cellar) lives in DungeonTrapBasement, shared with the
// TrapGauntlet obstacle so a fall behaves identically no matter which system placed the trap.
public class DungeonTrapGenerator(IOptionsSnapshot<TrapOptions> optionsSnapshot)
{
    private static readonly Dictionary<RoomRole, TrapKind> TrapKindByRole = new()
    {
        [RoomRole.TreasureRoom] = TrapKind.Mechanical,
        [RoomRole.CollapsedGallery] = TrapKind.Collapse,
        [RoomRole.Passage] = TrapKind.Slope,
        [RoomRole.FloodedSump] = TrapKind.Water,
    };

    internal DungeonTrapResult Generate(
        IReadOnlyList<DungeonRoomPlacement> placements,
        Guid worldId,
        Guid stateId,
        Random random
    )
    {
        var options = optionsSnapshot.Value;

        var chosen = placements
            .Where(placement => TrapKindByRole.ContainsKey(placement.Role))
            .OrderBy(_ => random.Next())
            .Take(options.MaxTrapsPerDungeon)
            .ToArray();

        if (chosen.Length == 0)
        {
            return new DungeonTrapResult([], [], [], []);
        }

        // A trap's target is never another trap room, or a chained fall between two unnoticed
        // traps could never resolve on its own. Only relevant to Water, which is the only kind
        // that can still target an existing room.
        var trapLocationIds = chosen.Select(placement => placement.Room.LocationId).ToHashSet();
        var buildingId = placements[0].Room.BuildingId;
        var basement = new DungeonTrapBasement(worldId, stateId, buildingId);

        var triggers = new List<Trigger>();
        foreach (var placement in chosen)
        {
            var kind = TrapKindByRole[placement.Role];
            var targetId =
                kind == TrapKind.Water
                    ? ChooseWaterTarget(placement, placements, trapLocationIds, random)
                    : basement.TargetFor(kind, placement);

            if (targetId == null)
            {
                continue;
            }

            triggers.Add(
                new Trigger
                {
                    LocationId = placement.Room.LocationId,
                    WorldId = worldId,
                    Name = TrapName(kind),
                    Description = TrapDescription(kind),
                    TrapKind = kind,
                    TargetId = targetId,
                }
            );
        }

        return new DungeonTrapResult(
            triggers,
            basement.Rooms,
            basement.Locations,
            basement.LocationConnectors
        );
    }

    // Water is the only mechanism that plausibly moves you backwards, sweeping you shallower on
    // the same floor rather than down. A dungeon layout that offers no candidate in that direction
    // simply gets no trap here.
    private static Guid? ChooseWaterTarget(
        DungeonRoomPlacement placement,
        IReadOnlyList<DungeonRoomPlacement> placements,
        IReadOnlySet<Guid> trapLocationIds,
        Random random
    )
    {
        var candidates = placements
            .Where(other => other.Room.LocationId != placement.Room.LocationId)
            .Where(other => !trapLocationIds.Contains(other.Room.LocationId))
            .Where(other => other.FloorNumber == placement.FloorNumber)
            .Where(other => other.DepthFromEntrance < placement.DepthFromEntrance)
            .ToArray();

        return candidates.Length == 0
            ? null
            : candidates[random.Next(candidates.Length)].Room.LocationId;
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
}
