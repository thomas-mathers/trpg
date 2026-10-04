using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record RoomAreaLimits(double MinimumArea, double MaximumArea);

internal static class RoomSizeCatalog
{
    private static readonly Dictionary<BuildingType, RoomAreaLimits> ByBuildingType = new()
    {
        [BuildingType.Cave] = new(MinimumArea: 20, MaximumArea: 200),
        [BuildingType.Crypt] = new(MinimumArea: 15, MaximumArea: 120),
        [BuildingType.Mine] = new(MinimumArea: 15, MaximumArea: 150),
        [BuildingType.Ruins] = new(MinimumArea: 20, MaximumArea: 150),
        [BuildingType.Tower] = new(MinimumArea: 12, MaximumArea: 60),
    };

    private static readonly Dictionary<RoomRole, RoomAreaLimits> ByRoomRole = new()
    {
        [RoomRole.Entrance] = new(MinimumArea: 20, MaximumArea: 60),
        [RoomRole.BossChamber] = new(MinimumArea: 80, MaximumArea: 200),
        [RoomRole.Passage] = new(MinimumArea: 8, MaximumArea: 24),
        [RoomRole.GuardPost] = new(MinimumArea: 16, MaximumArea: 40),
        [RoomRole.Storeroom] = new(MinimumArea: 12, MaximumArea: 40),
        [RoomRole.TreasureRoom] = new(MinimumArea: 16, MaximumArea: 50),
        [RoomRole.Shrine] = new(MinimumArea: 20, MaximumArea: 60),
        [RoomRole.Study] = new(MinimumArea: 16, MaximumArea: 45),
        [RoomRole.CellBlock] = new(MinimumArea: 30, MaximumArea: 100),
        [RoomRole.CollapsedGallery] = new(MinimumArea: 30, MaximumArea: 90),
        [RoomRole.FloodedSump] = new(MinimumArea: 20, MaximumArea: 70),
    };

    internal static RoomAreaLimits Get(BuildingType buildingType, RoomRole? role) =>
        role is { } roomRole ? ByRoomRole[roomRole] : ByBuildingType[buildingType];
}
