using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

// A trap's kind is implied entirely by its room's role, so there is no separate kind roll: a room
// already reads as a hazard (CollapsedGallery, FloodedSump) or a place worth guarding
// (TreasureRoom), and the trap makes that mechanical rather than only decorative.
public class DungeonTrapGenerator(IOptionsSnapshot<TrapOptions> optionsSnapshot)
{
    private static readonly Dictionary<RoomRole, TrapKind> TrapKindByRole = new()
    {
        [RoomRole.TreasureRoom] = TrapKind.Mechanical,
        [RoomRole.CollapsedGallery] = TrapKind.Collapse,
        [RoomRole.Passage] = TrapKind.Slope,
        [RoomRole.FloodedSump] = TrapKind.Water,
    };

    internal IReadOnlyList<Trigger> Generate(
        IReadOnlyList<DungeonRoomPlacement> placements,
        Guid worldId,
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
            return [];
        }

        // A trap's target is never another trap room, or a chained fall between two unnoticed
        // traps could never resolve on its own.
        var trapLocationIds = chosen.Select(placement => placement.Room.LocationId).ToHashSet();

        var traps = new List<Trigger>();
        foreach (var placement in chosen)
        {
            var kind = TrapKindByRole[placement.Role];
            var targetId = ChooseTarget(placement, kind, placements, trapLocationIds, random);
            if (targetId == null)
            {
                continue;
            }

            traps.Add(
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

        return traps;
    }

    // Water is the only mechanism that plausibly moves you backwards; the other three move you
    // deeper. A dungeon layout that offers no candidate in that direction simply gets no trap here.
    private static Guid? ChooseTarget(
        DungeonRoomPlacement placement,
        TrapKind kind,
        IReadOnlyList<DungeonRoomPlacement> placements,
        IReadOnlySet<Guid> trapLocationIds,
        Random random
    )
    {
        var candidates = placements
            .Where(other => other.Room.LocationId != placement.Room.LocationId)
            .Where(other => !trapLocationIds.Contains(other.Room.LocationId))
            .Where(other =>
                kind == TrapKind.Water
                    ? other.DepthFromEntrance < placement.DepthFromEntrance
                    : other.DepthFromEntrance > placement.DepthFromEntrance
            )
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
