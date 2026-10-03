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
