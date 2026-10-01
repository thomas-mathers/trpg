using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record RoomAreaLimits(double MinimumArea, double MaximumArea);

internal static class RoomSizeCatalog
{
    private static readonly Dictionary<BuildingType, RoomAreaLimits> ByBuildingType = new()
    {
        [BuildingType.ArcaneShop] = new(MinimumArea: 15, MaximumArea: 45),
        [BuildingType.Apothecary] = new(MinimumArea: 15, MaximumArea: 40),
        [BuildingType.Bakery] = new(MinimumArea: 15, MaximumArea: 40),
        [BuildingType.Barracks] = new(MinimumArea: 25, MaximumArea: 70),
        [BuildingType.Blacksmith] = new(MinimumArea: 20, MaximumArea: 50),
        [BuildingType.Carpenter] = new(MinimumArea: 20, MaximumArea: 60),
        [BuildingType.Castle] = new(MinimumArea: 50, MaximumArea: 250),
        [BuildingType.Cave] = new(MinimumArea: 20, MaximumArea: 200),
        [BuildingType.Crypt] = new(MinimumArea: 15, MaximumArea: 120),
        [BuildingType.GeneralGoods] = new(MinimumArea: 20, MaximumArea: 60),
        [BuildingType.GuildHall] = new(MinimumArea: 40, MaximumArea: 120),
        [BuildingType.House] = new(MinimumArea: 9, MaximumArea: 30),
        [BuildingType.Inn] = new(MinimumArea: 20, MaximumArea: 80),
        [BuildingType.Jail] = new(MinimumArea: 12, MaximumArea: 60),
        [BuildingType.Jeweler] = new(MinimumArea: 12, MaximumArea: 30),
        [BuildingType.Library] = new(MinimumArea: 30, MaximumArea: 100),
        [BuildingType.Mine] = new(MinimumArea: 15, MaximumArea: 150),
        [BuildingType.Ruins] = new(MinimumArea: 20, MaximumArea: 150),
        [BuildingType.Stable] = new(MinimumArea: 25, MaximumArea: 80),
        [BuildingType.Tailor] = new(MinimumArea: 15, MaximumArea: 40),
        [BuildingType.Tavern] = new(MinimumArea: 30, MaximumArea: 90),
        [BuildingType.Temple] = new(MinimumArea: 40, MaximumArea: 150),
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
