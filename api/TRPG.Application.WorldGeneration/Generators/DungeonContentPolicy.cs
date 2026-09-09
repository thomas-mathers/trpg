using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal enum DungeonLootQuality
{
    None,
    Mundane,
    Valuable,
}

// Not every room is occupied and not every room pays. Constant combat flattens the pacing, and a
// dungeon where everything holds treasure makes none of it worth finding.
internal static class DungeonContentPolicy
{
    public static bool HoldsOccupants(RoomRole role, Random random) =>
        role switch
        {
            // Arriving somewhere you cannot retreat from before you have your bearings is a trap.
            RoomRole.Entrance => false,
            RoomRole.BossChamber => true,
            RoomRole.GuardPost => true,
            RoomRole.TreasureRoom => random.NextDouble() < 0.7,
            RoomRole.CellBlock => random.NextDouble() < 0.4,
            RoomRole.Passage => random.NextDouble() < 0.35,
            RoomRole.Storeroom
            or RoomRole.Shrine
            or RoomRole.Study
            or RoomRole.CollapsedGallery
            or RoomRole.FloodedSump => random.NextDouble() < 0.15,
        };

    public static DungeonLootQuality Holds(RoomRole role, Random random) =>
        role switch
        {
            RoomRole.BossChamber => DungeonLootQuality.Valuable,
            RoomRole.TreasureRoom => DungeonLootQuality.Valuable,
            RoomRole.Shrine => random.NextDouble() < 0.6
                ? DungeonLootQuality.Valuable
                : DungeonLootQuality.None,
            RoomRole.Storeroom => DungeonLootQuality.Mundane,
            RoomRole.Study => random.NextDouble() < 0.7
                ? DungeonLootQuality.Mundane
                : DungeonLootQuality.None,
            RoomRole.CellBlock => random.NextDouble() < 0.4
                ? DungeonLootQuality.Mundane
                : DungeonLootQuality.None,
            RoomRole.CollapsedGallery or RoomRole.FloodedSump => random.NextDouble() < 0.25
                ? DungeonLootQuality.Mundane
                : DungeonLootQuality.None,
            RoomRole.Entrance or RoomRole.Passage or RoomRole.GuardPost => DungeonLootQuality.None,
        };
}
