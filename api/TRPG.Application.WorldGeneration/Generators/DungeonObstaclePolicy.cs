using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

// A key implies someone rational placed the lock and still remembers where the key went — a fit
// for a dungeon type whose occupants lean toward goblins and other tool-users, and a poor fit for
// one that leans undead or feral, where a trap or a lone guardian reads truer than a lock. A lever
// is more excusable than a key even for a monster lair, since it can be ancient and mechanical
// rather than actively maintained.
internal static class DungeonObstaclePolicy
{
    private static readonly Dictionary<
        BuildingType,
        Dictionary<DungeonObstacleKind, double>
    > WeightsByDungeonType = new()
    {
        [BuildingType.Cave] = new()
        {
            [DungeonObstacleKind.KeyLock] = 0.20,
            [DungeonObstacleKind.Miniboss] = 0.35,
            [DungeonObstacleKind.TrapGauntlet] = 0.30,
            [DungeonObstacleKind.LeverShortcut] = 0.15,
        },
        [BuildingType.Crypt] = new()
        {
            [DungeonObstacleKind.KeyLock] = 0.05,
            [DungeonObstacleKind.Miniboss] = 0.35,
            [DungeonObstacleKind.TrapGauntlet] = 0.45,
            [DungeonObstacleKind.LeverShortcut] = 0.15,
        },
        [BuildingType.Mine] = new()
        {
            [DungeonObstacleKind.KeyLock] = 0.25,
            [DungeonObstacleKind.Miniboss] = 0.25,
            [DungeonObstacleKind.TrapGauntlet] = 0.30,
            [DungeonObstacleKind.LeverShortcut] = 0.20,
        },
        [BuildingType.Ruins] = new()
        {
            [DungeonObstacleKind.KeyLock] = 0.05,
            [DungeonObstacleKind.Miniboss] = 0.40,
            [DungeonObstacleKind.TrapGauntlet] = 0.40,
            [DungeonObstacleKind.LeverShortcut] = 0.15,
        },
        [BuildingType.Tower] = new()
        {
            [DungeonObstacleKind.KeyLock] = 0.10,
            [DungeonObstacleKind.Miniboss] = 0.35,
            [DungeonObstacleKind.TrapGauntlet] = 0.30,
            [DungeonObstacleKind.LeverShortcut] = 0.25,
        },
    };

    public static DungeonObstacleKind ChooseKind(BuildingType dungeonType, Random random)
    {
        var weights = WeightsByDungeonType[dungeonType];
        var roll = random.NextDouble() * weights.Values.Sum();

        var cumulative = 0.0;
        foreach (var (kind, weight) in weights)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                return kind;
            }
        }

        return weights.Keys.Last();
    }
}
