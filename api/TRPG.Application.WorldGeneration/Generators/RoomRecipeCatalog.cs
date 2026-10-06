using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class RoomRecipeCatalog
{
    private static readonly RoomRecipe InnLobby = new([
        new CounterAt(0.8),
        new Anchored(PropModel.FurnitureFireplace, 1, 0.5, RecipeWall.East),
        new RugBeside(RecipeWall.East, 1.6, 2.4),
        new WallRun(PropModel.SeatBench, RecipeWall.West, From: 0.15, To: 0.8),
        new TableGrid(Top: 4, Bottom: 5),
        new CenteredAt(PropModel.FurnitureChandelier, 0.5, 0.5),
    ]);

    private static readonly RoomRecipe CommonRoom = new([
        new Anchored(PropModel.WorkstationCooking, 0, 0.5, RecipeWall.West),
        new RugBeside(RecipeWall.West, 1.6, 2.4),
        new CounterAt(0.3, RecipeWall.East),
        new TableGrid(Top: 3.5, Bottom: 4.5),
        new WallRun(PropModel.ContainerBarrel, RecipeWall.South, From: 0.65, To: 0.95),
        new CenteredAt(PropModel.FurnitureChandelier, 0.5, 0.5),
        new WallRun(PropModel.FurnitureWallSconce, RecipeWall.North, From: 0.2, To: 0.8, Count: 2),
    ]);

    private static readonly RoomRecipe LivingRoom = new([
        new Anchored(PropModel.FurnitureFireplace, 0, 0.5, RecipeWall.West),
        new RugBeside(RecipeWall.West, 1.6, 2.4),
        new Anchored(PropModel.FurnitureBookcase, 0, 0.1, RecipeWall.West),
        new Anchored(PropModel.SeatBench, 1, 0.55, RecipeWall.East),
        new TableSetAt(0.5, 0.42),
        new WallRun(PropModel.FurnitureWallSconce, RecipeWall.North, From: 0.3, To: 0.7, Count: 1),
    ]);

    private static readonly RoomRecipe GuildHall = new([
        new CounterAt(0.05),
        new WallRun(PropModel.FurnitureNoticeBoard, RecipeWall.West, From: 0.2, To: 0.8, Count: 3),
        new WallRun(PropModel.SeatBench, RecipeWall.East, From: 0.2, To: 0.8, Count: 3),
        new TableGrid(Top: 4, Bottom: 5),
        new CenteredAt(PropModel.FurnitureChandelier, 0.5, 0.5),
        new WallRun(PropModel.FurnitureWallSconce, RecipeWall.North, From: 0.4, To: 0.9, Count: 2),
    ]);

    private static readonly RoomRecipe GreatHall = new([
        new RugRunner(Width: 2.4, Top: 2.8, BottomInset: 5.3),
        new CenteredFromSouth(PropModel.SeatThrone, 4.6, RecipeWall.South),
        new CenteredFromSouth(PropModel.SeatThrone, 6.5, RecipeWall.South),
        new WallRun(PropModel.FurnitureFireplace, RecipeWall.West, From: 0.25, To: 0.75, Count: 2),
        new WallRun(PropModel.FurnitureFireplace, RecipeWall.East, From: 0.25, To: 0.75, Count: 2),
        new TableGrid(Top: 5, Bottom: 8),
        new CenteredAt(PropModel.FurnitureChandelier, 0.5, 0.3),
        new CenteredAt(PropModel.FurnitureChandelier, 0.5, 0.55),
    ]);

    private static readonly RoomRecipe GuardStation = new([
        new WallRun(PropModel.ContainerWeaponRack, RecipeWall.West, From: 0.2, To: 0.7, Count: 3),
        new WallRun(PropModel.SeatBench, RecipeWall.East, From: 0.3, To: 0.7, Count: 2),
        new TableSetAt(0.5, 0.45),
        new WallRun(PropModel.FurnitureWallSconce, RecipeWall.North, From: 0.2, To: 0.8, Count: 2),
    ]);

    private static readonly RoomRecipe JailCells = new([
        new Anchored(PropModel.ContainerChest, 0, 0),
        new Anchored(PropModel.ContainerChest, 0, 1),
        new Anchored(PropModel.Cell, 1, 0),
        new Anchored(PropModel.Cell, 1, 0.35),
        new Anchored(PropModel.Cell, 1, 0.7),
        new CenteredAt(PropModel.FurnitureChandelier, 0.4, 0.5),
    ]);

    private static readonly RoomRecipe DrillHall = new([
        new WallRun(PropModel.ContainerWeaponRack, RecipeWall.West, From: 0.1, To: 0.9, Count: 4),
        new WallRun(PropModel.ContainerWeaponRack, RecipeWall.East, From: 0.1, To: 0.9, Count: 4),
        new WallRun(PropModel.SeatBench, RecipeWall.North, From: 0.05, To: 0.95, Count: 4),
        new Anchored(PropModel.FurnitureTrainingDummy, 0.3, 0.45),
        new Anchored(PropModel.FurnitureTrainingDummy, 0.5, 0.45),
        new Anchored(PropModel.FurnitureTrainingDummy, 0.7, 0.45),
        new WallRun(PropModel.FurnitureWallSconce, RecipeWall.South, From: 0.2, To: 0.8, Count: 2),
    ]);

    private static readonly RoomRecipe Dormitory = new([
        new Anchored(PropModel.ContainerFootlocker, 0.5, 1, RecipeWall.South),
        new Anchored(PropModel.ContainerFootlocker, 0.5, 0),
        .. new[] { 0.0, 0.5, 1.0 }.SelectMany(row =>
            new RecipeStep[]
            {
                new Anchored(PropModel.Bed, 0, row),
                new Anchored(PropModel.Bed, 1, row),
            }
        ),
        new Anchored(PropModel.Bed, 0.5, 0, RecipeWall.East),
        new CenteredAt(PropModel.FurnitureChandelier, 0.5, 0.5),
    ]);

    private static readonly RoomRecipe Stable = new([
        new CounterAt(0.75),
        new WallRun(PropModel.FurnitureStall, RecipeWall.West, From: 0.1, To: 0.95),
        new WallRun(PropModel.FurnitureStall, RecipeWall.East, From: 0.1, To: 0.95),
        new WallRun(PropModel.ContainerBarrel, RecipeWall.South, From: 0.1, To: 0.3),
        new WallRun(PropModel.ContainerCrate, RecipeWall.South, From: 0.7, To: 0.9),
        new WallRun(PropModel.FurnitureWallSconce, RecipeWall.North, From: 0.1, To: 0.5, Count: 1),
    ]);

    private static readonly RoomRecipe Sanctuary = new([
        new RugRunner(Width: 1.4, Top: 2.8, BottomInset: 5),
        new CenteredFromSouth(PropModel.WorkstationPrayer, 4.2, RecipeWall.North),
        new Anchored(PropModel.WorkstationPrayer, 0.5, 0),
        new Anchored(PropModel.WorkstationPrayer, 0.15, 0),
        new PewRows(),
        new CenteredAt(PropModel.FurnitureChandelier, 0.5, 0.5),
    ]);

    private static readonly RoomRecipe LibraryRoom = new([
        new CounterAt(0.5),
        new CounterAt(0.3),
        new CounterAt(0.7),
        new Anchored(PropModel.WorkstationTrade, 0.5, 0.12),
        .. LibraryShelving(),
        new ReadingTables([0.15, 0.5, 0.85]),
        new CenteredAt(PropModel.FurnitureChandelier, 0.5, 0.5),
    ]);

    private static readonly RoomRecipe LibraryStudy = new([
        new Anchored(PropModel.Bed, 0, 1, RecipeWall.South),
        new Anchored(PropModel.Bed, 1, 1, RecipeWall.South),
        new Anchored(PropModel.ContainerChest, 0, 0.82, RecipeWall.West),
        new Anchored(PropModel.ContainerChest, 1, 0.82, RecipeWall.East),
        .. LibraryShelving(),
        new ReadingTables([0.15, 0.5, 0.85]),
        new CenteredAt(PropModel.FurnitureChandelier, 0.5, 0.5),
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
        new WallRun(PropModel.FurnitureWallSconce, RecipeWall.North, From: 0.25, To: 0.6, Count: 2),
    ]);

    private static readonly RoomRecipe HouseBedroom = new([
        new RugAt(0.5, 0.5, 1.2, 1.2),
        new Anchored(PropModel.Bed, 0, 0),
        new Anchored(PropModel.Bed, 1, 0),
        new Anchored(PropModel.Bed, 0, 1),
        new Anchored(PropModel.Bed, 1, 1),
        new Anchored(PropModel.ContainerChest, 0, 1),
        new Anchored(PropModel.ContainerChest, 1, 1),
        new Anchored(PropModel.ContainerChest, 1, 1, RecipeWall.East),
        new Anchored(PropModel.ContainerChest, 0, 1, RecipeWall.West),
        new Anchored(PropModel.ContainerChest, 1, 0.5),
        new Anchored(PropModel.ContainerChest, 0, 0.5),
        new WallRun(PropModel.FurnitureWallSconce, RecipeWall.North, From: 0.5, To: 0.5, Count: 1),
        new WallRun(PropModel.FurnitureWallSconce, RecipeWall.South, From: 0.5, To: 0.5, Count: 1),
    ]);

    private static readonly IReadOnlyDictionary<BuildingType, RoomRecipe> TradeRooms =
        new Dictionary<BuildingType, RoomRecipe>
        {
            [BuildingType.Blacksmith] = BlacksmithWorkshop,
            [BuildingType.GeneralGoods] = Shop(
                station: null,
                new WallRun(PropModel.ContainerCrate, RecipeWall.South, From: 0.05, To: 0.3),
                new WallRun(PropModel.ContainerBarrel, RecipeWall.South, From: 0.7, To: 0.95)
            ),
            [BuildingType.Apothecary] = Shop(
                PropModel.WorkstationAlchemy,
                new SouthStock(PropModel.FurnitureCauldron, PropModel.FurnitureHerbRack)
            ),
            [BuildingType.ArcaneShop] = Shop(
                PropModel.WorkstationEnchanting,
                new SouthStock(PropModel.FurnitureLectern, PropModel.FurnitureStaffRack)
            ),
            [BuildingType.Tailor] = Shop(
                PropModel.WorkstationTailoring,
                new SouthStock(PropModel.FurnitureMannequin, PropModel.FurnitureClothShelf)
            ),
            [BuildingType.Jeweler] = Shop(
                PropModel.WorkstationJewelcrafting,
                new SouthStock(Left: null, PropModel.FurnitureDisplayCase)
            ),
            [BuildingType.Bakery] = Shop(
                PropModel.WorkstationCooking,
                new WallRun(PropModel.FurnitureWorkTable, RecipeWall.East, From: 0.5, To: 0.95),
                new SouthStock(PropModel.FurnitureFlourSacks, PropModel.FurnitureBreadRack)
            ),
            [BuildingType.Carpenter] = Shop(
                PropModel.WorkstationCarpentry,
                new WallRun(PropModel.FurnitureWorkTable, RecipeWall.East, From: 0.5, To: 0.95),
                new SouthStock(PropModel.FurnitureLumberStack, PropModel.FurnitureTimberRack)
            ),
        };

    internal static RoomRecipe? Find(BuildingType buildingType, string roomName) =>
        BuildingTypes.Dungeon.Contains(buildingType)
            ? null
            : (buildingType, roomName) switch
            {
                (BuildingType.Inn, "Lobby") => InnLobby,
                (BuildingType.Inn, _)
                    when roomName.EndsWith("Guest Room", StringComparison.Ordinal) => WestBedroom,
                (BuildingType.Tavern, "Common Room") => CommonRoom,
                (BuildingType.House, "Living Room") => LivingRoom,
                (BuildingType.House, _)
                    when roomName.StartsWith("Bedroom", StringComparison.Ordinal) => HouseBedroom,
                (BuildingType.GuildHall, "Hall") => GuildHall,
                (BuildingType.GuildHall, _) => WestBedroom,
                (BuildingType.Library, "Reading Room") => LibraryRoom,
                (BuildingType.Library, "Study") => LibraryStudy,
                (BuildingType.Temple, "Sanctuary") => Sanctuary,
                (BuildingType.Stable, "Stable") => Stable,
                (BuildingType.Barracks, "Drill Hall") => DrillHall,
                (BuildingType.Barracks, "Barracks Dormitory") => Dormitory,
                (BuildingType.Barracks, _) => WestBedroom,
                (BuildingType.Castle, "Great Hall") => GreatHall,
                (BuildingType.Jail, "Guard Station") => GuardStation,
                (BuildingType.Jail, JailRoomNames.Cells) => JailCells,
                (_, "Shop" or "Workshop" or "Bakery") => TradeRooms.GetValueOrDefault(buildingType),
                (_, "Living Quarters" or "Owner's Quarters" or "Quarters" or "Royal Chambers") =>
                    EastBedroom,
                _ => null,
            };

    private static RoomRecipe Shop(PropModel? station, params RecipeStep[] extras)
    {
        var steps = new List<RecipeStep>();

        if (station is { } model)
        {
            steps.Add(new Anchored(model, 0, 1));
        }

        steps.Add(new Anchored(PropModel.WorkstationTrade, 0.75, 0.3));
        steps.AddRange(extras);
        steps.Add(
            new WallRun(PropModel.FurnitureDisplayShelf, RecipeWall.West, From: 0.05, To: 0.8)
        );
        steps.Add(
            new WallRun(PropModel.FurnitureDisplayShelf, RecipeWall.East, From: 0.35, To: 0.95)
        );
        steps.Add(
            new WallRun(
                PropModel.FurnitureWallSconce,
                RecipeWall.North,
                From: 0.3,
                To: 0.7,
                Count: 2
            )
        );

        return new RoomRecipe(steps);
    }

    private static RecipeStep[] LibraryShelving() =>
        [
            new WallRun(PropModel.WorkstationReading, RecipeWall.West),
            new WallRun(PropModel.WorkstationReading, RecipeWall.East),
            new WallRun(PropModel.WorkstationReading, RecipeWall.North, From: 0.05, To: 0.35),
            new WallRun(PropModel.WorkstationReading, RecipeWall.North, From: 0.65, To: 0.95),
            new BookStacks([0.3, 0.7]),
        ];

    private static RoomRecipe Bedroom(double bedFraction)
    {
        var chairWall = bedFraction == 0 ? RecipeWall.East : RecipeWall.West;

        return new([
            new RugAt(0.5, 0.38, 1.5, 1.5),
            new CenteredAt(PropModel.FurnitureChandelier, 0.5, 0.5),
            new Anchored(PropModel.Bed, bedFraction, 0),
            new Anchored(PropModel.Bed, bedFraction, 1),
            new Anchored(PropModel.ContainerChest, bedFraction, 1),
            new Anchored(PropModel.ContainerChest, 1 - bedFraction, 1),
            new Anchored(PropModel.SeatChair, 1 - bedFraction, 0.3, chairWall),
            new Anchored(PropModel.SeatChair, 1 - bedFraction, 0.7, chairWall),
            new WallRun(
                PropModel.FurnitureWallSconce,
                RecipeWall.North,
                From: 0.5,
                To: 0.5,
                Count: 1
            ),
        ]);
    }
}
