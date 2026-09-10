using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record DungeonObstacleWeights(double KeyLock, double Miniboss, double TrapGauntlet);

// A key implies someone rational placed the lock and still remembers where the key went — a fit
// for a dungeon type whose occupants lean toward goblins and other tool-users, and a poor fit for
// one that leans undead or feral, where a trap or a lone guardian reads truer than a lock.
internal static class DungeonObstaclePolicy
{
    private static readonly Dictionary<BuildingType, DungeonObstacleWeights> WeightsByDungeonType =
        new()
        {
            [BuildingType.Cave] = new DungeonObstacleWeights(
                KeyLock: 0.25,
                Miniboss: 0.40,
                TrapGauntlet: 0.35
            ),
            [BuildingType.Crypt] = new DungeonObstacleWeights(
                KeyLock: 0.05,
                Miniboss: 0.40,
                TrapGauntlet: 0.55
            ),
            [BuildingType.Mine] = new DungeonObstacleWeights(
                KeyLock: 0.30,
                Miniboss: 0.30,
                TrapGauntlet: 0.40
            ),
            [BuildingType.Ruins] = new DungeonObstacleWeights(
                KeyLock: 0.05,
                Miniboss: 0.45,
                TrapGauntlet: 0.50
            ),
            [BuildingType.Tower] = new DungeonObstacleWeights(
                KeyLock: 0.15,
                Miniboss: 0.45,
                TrapGauntlet: 0.40
            ),
        };

    public static DungeonObstacleKind ChooseKind(BuildingType dungeonType, Random random)
    {
        var weights = WeightsByDungeonType[dungeonType];
        var roll =
            random.NextDouble() * (weights.KeyLock + weights.Miniboss + weights.TrapGauntlet);

        if (roll < weights.KeyLock)
        {
            return DungeonObstacleKind.KeyLock;
        }

        return roll < weights.KeyLock + weights.Miniboss
            ? DungeonObstacleKind.Miniboss
            : DungeonObstacleKind.TrapGauntlet;
    }
}
