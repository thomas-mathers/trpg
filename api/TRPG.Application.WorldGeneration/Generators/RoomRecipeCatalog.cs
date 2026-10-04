using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class RoomRecipeCatalog
{
    private static readonly RoomRecipe InnLobby = new([
        new Anchored(PropModel.WorkstationTrade, 0.8, 0),
        new Anchored(PropModel.FurnitureFireplace, 1, 0.5, RecipeWall.East),
        new RugBeside(RecipeWall.East, 1.6, 2.4),
        new TableGrid(Top: 4, Bottom: 5),
    ]);

    private static readonly RoomRecipe WestBedroom = Bedroom(bedFraction: 0);

    private static readonly RoomRecipe EastBedroom = Bedroom(bedFraction: 1);

    private static readonly RoomRecipe BlacksmithWorkshop = new([
        new Anchored(PropModel.WorkstationWeaponsmithing, 0, 1),
        new Anchored(PropModel.WorkstationTrade, 0.75, 0.3),
        new Anchored(PropModel.ContainerChest, 1, 0),
        new Anchored(PropModel.WorkstationArmorsmithing, 0.25, 0.75),
        new WallRun(PropModel.FurnitureDisplayShelf, RecipeWall.West, From: 0.05, To: 0.8),
        new WallRun(PropModel.FurnitureDisplayShelf, RecipeWall.East, From: 0.35, To: 0.95),
    ]);

    internal static RoomRecipe? Find(BuildingType buildingType, string roomName) =>
        (buildingType, roomName) switch
        {
            (BuildingType.Inn, "Lobby") => InnLobby,
            (BuildingType.Inn, "Owner's Quarters") => EastBedroom,
            (BuildingType.Inn, _) when roomName.EndsWith("Guest Room", StringComparison.Ordinal) =>
                WestBedroom,
            (BuildingType.Blacksmith, "Workshop") => BlacksmithWorkshop,
            (BuildingType.Blacksmith, "Living Quarters") => EastBedroom,
            _ => null,
        };

    private static RoomRecipe Bedroom(double bedFraction) =>
        new([
            new RugAt(0.5, 0.38, 1.5, 1.5),
            new Anchored(PropModel.Bed, bedFraction, 0),
            new Anchored(PropModel.ContainerChest, bedFraction, 1),
            new Anchored(PropModel.SeatChair, 1 - bedFraction, 0),
        ]);
}
