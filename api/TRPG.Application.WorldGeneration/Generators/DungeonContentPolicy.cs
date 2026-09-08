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
    public static bool HoldsOccupants(DungeonRoomRole role, Random random) =>
        role switch
        {
            // Arriving somewhere you cannot retreat from before you have your bearings is a trap.
            DungeonRoomRole.Entrance => false,
            DungeonRoomRole.BossChamber => true,
            DungeonRoomRole.GuardPost => true,
            DungeonRoomRole.TreasureRoom => random.NextDouble() < 0.7,
            DungeonRoomRole.CellBlock => random.NextDouble() < 0.4,
            DungeonRoomRole.Passage => random.NextDouble() < 0.35,
            DungeonRoomRole.Storeroom
            or DungeonRoomRole.Shrine
            or DungeonRoomRole.Study
            or DungeonRoomRole.CollapsedGallery
            or DungeonRoomRole.FloodedSump => random.NextDouble() < 0.15,
        };

    public static DungeonLootQuality Holds(DungeonRoomRole role, Random random) =>
        role switch
        {
            DungeonRoomRole.BossChamber => DungeonLootQuality.Valuable,
            DungeonRoomRole.TreasureRoom => DungeonLootQuality.Valuable,
            DungeonRoomRole.Shrine => random.NextDouble() < 0.6
                ? DungeonLootQuality.Valuable
                : DungeonLootQuality.None,
            DungeonRoomRole.Storeroom => DungeonLootQuality.Mundane,
            DungeonRoomRole.Study => random.NextDouble() < 0.7
                ? DungeonLootQuality.Mundane
                : DungeonLootQuality.None,
            DungeonRoomRole.CellBlock => random.NextDouble() < 0.4
                ? DungeonLootQuality.Mundane
                : DungeonLootQuality.None,
            DungeonRoomRole.CollapsedGallery or DungeonRoomRole.FloodedSump => random.NextDouble()
            < 0.25
                ? DungeonLootQuality.Mundane
                : DungeonLootQuality.None,
            DungeonRoomRole.Entrance or DungeonRoomRole.Passage or DungeonRoomRole.GuardPost =>
                DungeonLootQuality.None,
        };
}
