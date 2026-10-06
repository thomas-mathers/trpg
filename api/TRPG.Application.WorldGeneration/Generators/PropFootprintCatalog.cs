using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal enum PropPlacementRule
{
    Corner,
    Wall,
    Anchor,
    Center,
    Free,
}

internal record PropFootprintSpec(
    double Width,
    double Depth,
    PropPlacementRule Rule,
    double FrontClearance
)
{
    public Footprint Footprint => new(Width, Depth);
}

internal static class PropFootprintCatalog
{
    private static readonly Dictionary<PropModel, PropFootprintSpec> Specs = new()
    {
        [PropModel.Bed] = new(
            Width: 1.0,
            Depth: 2.0,
            PropPlacementRule.Corner,
            FrontClearance: 0.6
        ),
        [PropModel.Cell] = new(
            Width: 2.0,
            Depth: 2.0,
            PropPlacementRule.Corner,
            FrontClearance: 0.8
        ),
        [PropModel.Sign] = new(Width: 0.5, Depth: 0.2, PropPlacementRule.Wall, FrontClearance: 0.5),
        [PropModel.ContainerBasic] = new(
            Width: 0.8,
            Depth: 0.6,
            PropPlacementRule.Wall,
            FrontClearance: 0.6
        ),
        [PropModel.ContainerBarrel] = new(
            Width: 0.6,
            Depth: 0.6,
            PropPlacementRule.Wall,
            FrontClearance: 0.3
        ),
        [PropModel.ContainerChest] = new(
            Width: 0.9,
            Depth: 0.6,
            PropPlacementRule.Wall,
            FrontClearance: 0.6
        ),
        [PropModel.ContainerCrate] = new(
            Width: 0.7,
            Depth: 0.7,
            PropPlacementRule.Wall,
            FrontClearance: 0.3
        ),
        [PropModel.ContainerFootlocker] = new(
            Width: 0.9,
            Depth: 0.5,
            PropPlacementRule.Wall,
            FrontClearance: 0.3
        ),
        [PropModel.ContainerStrongbox] = new(
            Width: 0.8,
            Depth: 0.5,
            PropPlacementRule.Wall,
            FrontClearance: 0.6
        ),
        [PropModel.ContainerWeaponRack] = new(
            Width: 1.2,
            Depth: 0.4,
            PropPlacementRule.Wall,
            FrontClearance: 0.6
        ),
        [PropModel.FurnitureTable] = new(
            Width: 1.8,
            Depth: 1.0,
            PropPlacementRule.Free,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureWorkTable] = new(
            Width: 2.4,
            Depth: 1.2,
            PropPlacementRule.Wall,
            FrontClearance: 0.8
        ),
        [PropModel.FurnitureFireplace] = new(
            Width: 1.4,
            Depth: 0.8,
            PropPlacementRule.Wall,
            FrontClearance: 0.8
        ),
        [PropModel.FurnitureStall] = new(
            Width: 2.4,
            Depth: 2.4,
            PropPlacementRule.Free,
            FrontClearance: 0.8
        ),
        [PropModel.FurnitureNoticeBoard] = new(
            Width: 2.0,
            Depth: 0.25,
            PropPlacementRule.Wall,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureDisplayShelf] = new(
            Width: 1.2,
            Depth: 0.5,
            PropPlacementRule.Wall,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureTrainingDummy] = new(
            Width: 0.7,
            Depth: 0.7,
            PropPlacementRule.Free,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureRug] = new(
            Width: 1.6,
            Depth: 2.4,
            PropPlacementRule.Free,
            FrontClearance: 0.0
        ),
        [PropModel.FurnitureCauldron] = new(
            Width: 0.9,
            Depth: 0.9,
            PropPlacementRule.Wall,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureHerbRack] = new(
            Width: 1.4,
            Depth: 0.4,
            PropPlacementRule.Wall,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureLectern] = new(
            Width: 0.7,
            Depth: 0.6,
            PropPlacementRule.Wall,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureStaffRack] = new(
            Width: 1.2,
            Depth: 0.4,
            PropPlacementRule.Wall,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureMannequin] = new(
            Width: 0.5,
            Depth: 0.5,
            PropPlacementRule.Free,
            FrontClearance: 0.4
        ),
        [PropModel.FurnitureClothShelf] = new(
            Width: 1.4,
            Depth: 0.5,
            PropPlacementRule.Wall,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureDisplayCase] = new(
            Width: 1.4,
            Depth: 0.6,
            PropPlacementRule.Wall,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureFlourSacks] = new(
            Width: 0.8,
            Depth: 0.6,
            PropPlacementRule.Wall,
            FrontClearance: 0.3
        ),
        [PropModel.FurnitureBreadRack] = new(
            Width: 1.4,
            Depth: 0.5,
            PropPlacementRule.Wall,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureLumberStack] = new(
            Width: 1.8,
            Depth: 0.7,
            PropPlacementRule.Wall,
            FrontClearance: 0.4
        ),
        [PropModel.FurnitureTimberRack] = new(
            Width: 1.4,
            Depth: 0.4,
            PropPlacementRule.Wall,
            FrontClearance: 0.4
        ),
        [PropModel.FurnitureBookcase] = new(
            Width: 1.0,
            Depth: 0.7,
            PropPlacementRule.Wall,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureChair] = new(
            Width: 0.5,
            Depth: 0.5,
            PropPlacementRule.Free,
            FrontClearance: 0.3
        ),
        [PropModel.FurnitureBench] = new(
            Width: 1.5,
            Depth: 0.5,
            PropPlacementRule.Wall,
            FrontClearance: 0.4
        ),
        [PropModel.FurniturePew] = new(
            Width: 2.0,
            Depth: 0.6,
            PropPlacementRule.Free,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureFountain] = new(
            Width: 3.2,
            Depth: 3.2,
            PropPlacementRule.Center,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureWell] = new(
            Width: 1.4,
            Depth: 1.4,
            PropPlacementRule.Center,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureFirePit] = new(
            Width: 1.6,
            Depth: 1.6,
            PropPlacementRule.Center,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureStatue] = new(
            Width: 1.2,
            Depth: 1.2,
            PropPlacementRule.Center,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureMonument] = new(
            Width: 1.8,
            Depth: 1.8,
            PropPlacementRule.Center,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureShrine] = new(
            Width: 1.4,
            Depth: 1.4,
            PropPlacementRule.Center,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureWaystone] = new(
            Width: 1.2,
            Depth: 1.2,
            PropPlacementRule.Center,
            FrontClearance: 0.5
        ),
        [PropModel.FurnitureTree] = new(2.4, 2.4, PropPlacementRule.Free, 0.5),
        [PropModel.FurnitureShrub] = new(1.0, 1.0, PropPlacementRule.Free, 0.2),
        [PropModel.FurnitureFlowerBed] = new(2.4, 1.2, PropPlacementRule.Free, 0.3),
        [PropModel.FurnitureHerbTub] = new(1.0, 1.0, PropPlacementRule.Free, 0.3),
        [PropModel.FurnitureStreetLantern] = new(0.6, 0.6, PropPlacementRule.Free, 0.2),
        [PropModel.FurnitureWallLantern] = new(0.6, 0.6, PropPlacementRule.Wall, 0.1),
        [PropModel.FurnitureHitchingRail] = new(2.0, 0.4, PropPlacementRule.Free, 0.5),
        [PropModel.FurnitureCart] = new(2.4, 1.4, PropPlacementRule.Free, 0.6),
        [PropModel.FurnitureTrough] = new(1.8, 0.8, PropPlacementRule.Free, 0.4),
        [PropModel.FurnitureBanner] = new(0.9, 0.3, PropPlacementRule.Free, 0.3),
        [PropModel.FurnitureChandelier] = new(0.9, 0.9, PropPlacementRule.Center, 0.0),
        [PropModel.FurnitureWallSconce] = new(0.3, 0.2, PropPlacementRule.Wall, 0.0),
        [PropModel.SeatBasic] = new(
            Width: 0.5,
            Depth: 0.5,
            PropPlacementRule.Anchor,
            FrontClearance: 0.3
        ),
        [PropModel.SeatChair] = new(
            Width: 0.5,
            Depth: 0.5,
            PropPlacementRule.Anchor,
            FrontClearance: 0.3
        ),
        [PropModel.SeatPew] = new(
            Width: 2.0,
            Depth: 0.6,
            PropPlacementRule.Center,
            FrontClearance: 0.5
        ),
        [PropModel.SeatThrone] = new(
            Width: 0.9,
            Depth: 0.9,
            PropPlacementRule.Wall,
            FrontClearance: 0.8
        ),
        [PropModel.SeatBench] = new(
            Width: 1.5,
            Depth: 0.5,
            PropPlacementRule.Wall,
            FrontClearance: 0.4
        ),
        [PropModel.SeatStoneBench] = new(
            Width: 1.6,
            Depth: 0.6,
            PropPlacementRule.Wall,
            FrontClearance: 0.4
        ),
        [PropModel.SeatLowWall] = new(
            Width: 2.0,
            Depth: 0.4,
            PropPlacementRule.Wall,
            FrontClearance: 0.4
        ),
        [PropModel.TrapMechanical] = new(
            Width: 1.5,
            Depth: 1.5,
            PropPlacementRule.Center,
            FrontClearance: 0.0
        ),
        [PropModel.TrapCollapse] = new(
            Width: 1.5,
            Depth: 1.5,
            PropPlacementRule.Center,
            FrontClearance: 0.0
        ),
        [PropModel.TrapSlope] = new(
            Width: 1.5,
            Depth: 1.5,
            PropPlacementRule.Center,
            FrontClearance: 0.0
        ),
        [PropModel.TrapWater] = new(
            Width: 1.5,
            Depth: 1.5,
            PropPlacementRule.Center,
            FrontClearance: 0.0
        ),
        [PropModel.TriggerBasic] = new(
            Width: 0.4,
            Depth: 0.4,
            PropPlacementRule.Wall,
            FrontClearance: 0.5
        ),
        [PropModel.TriggerLever] = new(
            Width: 0.3,
            Depth: 0.3,
            PropPlacementRule.Wall,
            FrontClearance: 0.5
        ),
        [PropModel.WorkstationAlchemy] = new(
            Width: 1.2,
            Depth: 0.7,
            PropPlacementRule.Wall,
            FrontClearance: 0.8
        ),
        [PropModel.WorkstationArmorsmithing] = new(
            Width: 1.4,
            Depth: 1.2,
            PropPlacementRule.Center,
            FrontClearance: 0.8
        ),
        [PropModel.WorkstationCarpentry] = new(
            Width: 1.6,
            Depth: 0.8,
            PropPlacementRule.Wall,
            FrontClearance: 0.8
        ),
        [PropModel.WorkstationCooking] = new(
            Width: 1.4,
            Depth: 0.8,
            PropPlacementRule.Wall,
            FrontClearance: 0.8
        ),
        [PropModel.WorkstationEnchanting] = new(
            Width: 1.2,
            Depth: 1.2,
            PropPlacementRule.Center,
            FrontClearance: 0.8
        ),
        [PropModel.WorkstationJewelcrafting] = new(
            Width: 1.2,
            Depth: 0.7,
            PropPlacementRule.Wall,
            FrontClearance: 0.6
        ),
        [PropModel.WorkstationPrayer] = new(
            Width: 1.2,
            Depth: 0.8,
            PropPlacementRule.Wall,
            FrontClearance: 0.8
        ),
        [PropModel.WorkstationReading] = new(
            Width: 1.0,
            Depth: 0.7,
            PropPlacementRule.Wall,
            FrontClearance: 0.6
        ),
        [PropModel.WorkstationTailoring] = new(
            Width: 1.4,
            Depth: 0.8,
            PropPlacementRule.Wall,
            FrontClearance: 0.8
        ),
        [PropModel.WorkstationTrade] = new(
            Width: 1.8,
            Depth: 0.8,
            PropPlacementRule.Center,
            FrontClearance: 0.8
        ),
        [PropModel.WorkstationWeaponsmithing] = new(
            Width: 1.4,
            Depth: 1.2,
            PropPlacementRule.Wall,
            FrontClearance: 1.0
        ),
    };

    internal static IReadOnlyCollection<PropModel> Models => Specs.Keys;

    internal static PropFootprintSpec Get(PropModel model) =>
        Specs.TryGetValue(model, out var spec)
            ? spec
            : throw new InvalidOperationException($"No footprint is cataloged for {model}.");
}
