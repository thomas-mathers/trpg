using Tapper;

namespace TRPG.GameSessions.Responses;

[TranspilationSource]
public record PlacementWire(double X, double Y, double Angle);

[TranspilationSource]
public record FootprintWire(double Width, double Depth);

[TranspilationSource]
public enum PropModel
{
    Bed,
    Cell,
    Sign,
    ContainerBasic,
    ContainerBarrel,
    ContainerChest,
    ContainerCrate,
    ContainerFootlocker,
    ContainerStrongbox,
    ContainerWeaponRack,
    SeatBasic,
    SeatChair,
    SeatPew,
    SeatThrone,
    SeatBench,
    SeatStoneBench,
    SeatLowWall,
    TrapMechanical,
    TrapCollapse,
    TrapSlope,
    TrapWater,
    TriggerBasic,
    TriggerLever,
    WorkstationAlchemy,
    WorkstationArmorsmithing,
    WorkstationCarpentry,
    WorkstationCooking,
    WorkstationEnchanting,
    WorkstationJewelcrafting,
    WorkstationPrayer,
    WorkstationReading,
    WorkstationTailoring,
    WorkstationTrade,
    WorkstationWeaponsmithing,
}

[TranspilationSource]
public record PropLayoutWire(
    Guid Id,
    PropModel Model,
    PlacementWire Placement,
    FootprintWire Footprint
);

[TranspilationSource]
public record BuildingLayoutWire(
    Guid Id,
    BuildingType Type,
    PlacementWire Placement,
    FootprintWire Footprint
);

[TranspilationSource]
public record ConnectorLayoutWire(
    Guid ConnectorId,
    Guid DestinationLocationId,
    double ExitX,
    double ExitY
);

[TranspilationSource]
public record CreatureLayoutWire(Guid Id, PlacementWire Placement);

[TranspilationSource]
public record LocationLayoutWire(
    FootprintWire Size,
    IReadOnlyCollection<PropLayoutWire> Props,
    IReadOnlyCollection<BuildingLayoutWire> Buildings,
    IReadOnlyCollection<ConnectorLayoutWire> Connectors,
    IReadOnlyCollection<CreatureLayoutWire> Creatures
);
