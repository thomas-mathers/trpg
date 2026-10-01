using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public static class PropModelResolver
{
    private static readonly Dictionary<string, PropModel> SeatsByName = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["Chair"] = PropModel.SeatChair,
        ["Pew"] = PropModel.SeatPew,
        ["Throne"] = PropModel.SeatThrone,
        ["Bench"] = PropModel.SeatBench,
        ["Stone Bench"] = PropModel.SeatStoneBench,
        ["Low Wall"] = PropModel.SeatLowWall,
    };

    private static readonly Dictionary<string, PropModel> ContainersByName = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["Barrel"] = PropModel.ContainerBarrel,
        ["Chest"] = PropModel.ContainerChest,
        ["Crate"] = PropModel.ContainerCrate,
        ["Footlocker"] = PropModel.ContainerFootlocker,
        ["Strongbox"] = PropModel.ContainerStrongbox,
        ["Weapon Rack"] = PropModel.ContainerWeaponRack,
    };

    private static readonly Dictionary<string, PropModel> TriggersByName = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["Lever"] = PropModel.TriggerLever,
    };

    public static PropModel Resolve(Prop prop) =>
        prop switch
        {
            Workstation workstation => ResolveWorkstation(workstation.WorkstationType),
            Seat seat => ResolveByName(SeatsByName, seat.Name, PropModel.SeatBasic),
            Container container => ResolveByName(
                ContainersByName,
                container.Name,
                PropModel.ContainerBasic
            ),
            Trigger trigger => ResolveByName(TriggersByName, trigger.Name, PropModel.TriggerBasic),
            Trap trap => ResolveTrap(trap.TrapKind),
            Bed => PropModel.Bed,
            Cell => PropModel.Cell,
            Sign => PropModel.Sign,
            _ => throw new InvalidOperationException($"{prop.GetType().Name} has no prop model."),
        };

    private static PropModel ResolveByName(
        Dictionary<string, PropModel> modelsByName,
        string name,
        PropModel fallback
    ) => modelsByName.GetValueOrDefault(name.Trim(), fallback);

    private static PropModel ResolveTrap(TrapKind kind) =>
        kind switch
        {
            TrapKind.Mechanical => PropModel.TrapMechanical,
            TrapKind.Collapse => PropModel.TrapCollapse,
            TrapKind.Slope => PropModel.TrapSlope,
            TrapKind.Water => PropModel.TrapWater,
            _ => throw new InvalidOperationException($"{kind} has no prop model."),
        };

    private static PropModel ResolveWorkstation(WorkstationType type) =>
        type switch
        {
            WorkstationType.Alchemy => PropModel.WorkstationAlchemy,
            WorkstationType.Armorsmithing => PropModel.WorkstationArmorsmithing,
            WorkstationType.Carpentry => PropModel.WorkstationCarpentry,
            WorkstationType.Cooking => PropModel.WorkstationCooking,
            WorkstationType.Enchanting => PropModel.WorkstationEnchanting,
            WorkstationType.Jewelcrafting => PropModel.WorkstationJewelcrafting,
            WorkstationType.Tailoring => PropModel.WorkstationTailoring,
            WorkstationType.Trade => PropModel.WorkstationTrade,
            WorkstationType.Prayer => PropModel.WorkstationPrayer,
            WorkstationType.Reading => PropModel.WorkstationReading,
            WorkstationType.Weaponsmithing => PropModel.WorkstationWeaponsmithing,
            _ => throw new InvalidOperationException($"{type} has no prop model."),
        };
}
