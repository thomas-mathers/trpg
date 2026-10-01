using ContractPropModel = TRPG.GameSessions.Responses.PropModel;
using DataPropModel = TRPG.Domain.Models.PropModel;

namespace TRPG.GameSessions.Mappers;

internal static class PropModelMapper
{
    public static ContractPropModel ToResponse(this DataPropModel model) =>
        model switch
        {
            DataPropModel.Bed => ContractPropModel.Bed,
            DataPropModel.Cell => ContractPropModel.Cell,
            DataPropModel.Sign => ContractPropModel.Sign,
            DataPropModel.ContainerBasic => ContractPropModel.ContainerBasic,
            DataPropModel.ContainerBarrel => ContractPropModel.ContainerBarrel,
            DataPropModel.ContainerChest => ContractPropModel.ContainerChest,
            DataPropModel.ContainerCrate => ContractPropModel.ContainerCrate,
            DataPropModel.ContainerFootlocker => ContractPropModel.ContainerFootlocker,
            DataPropModel.ContainerStrongbox => ContractPropModel.ContainerStrongbox,
            DataPropModel.ContainerWeaponRack => ContractPropModel.ContainerWeaponRack,
            DataPropModel.SeatBasic => ContractPropModel.SeatBasic,
            DataPropModel.SeatChair => ContractPropModel.SeatChair,
            DataPropModel.SeatPew => ContractPropModel.SeatPew,
            DataPropModel.SeatThrone => ContractPropModel.SeatThrone,
            DataPropModel.SeatBench => ContractPropModel.SeatBench,
            DataPropModel.SeatStoneBench => ContractPropModel.SeatStoneBench,
            DataPropModel.SeatLowWall => ContractPropModel.SeatLowWall,
            DataPropModel.TrapMechanical => ContractPropModel.TrapMechanical,
            DataPropModel.TrapCollapse => ContractPropModel.TrapCollapse,
            DataPropModel.TrapSlope => ContractPropModel.TrapSlope,
            DataPropModel.TrapWater => ContractPropModel.TrapWater,
            DataPropModel.TriggerBasic => ContractPropModel.TriggerBasic,
            DataPropModel.TriggerLever => ContractPropModel.TriggerLever,
            DataPropModel.WorkstationAlchemy => ContractPropModel.WorkstationAlchemy,
            DataPropModel.WorkstationArmorsmithing => ContractPropModel.WorkstationArmorsmithing,
            DataPropModel.WorkstationCarpentry => ContractPropModel.WorkstationCarpentry,
            DataPropModel.WorkstationCooking => ContractPropModel.WorkstationCooking,
            DataPropModel.WorkstationEnchanting => ContractPropModel.WorkstationEnchanting,
            DataPropModel.WorkstationJewelcrafting => ContractPropModel.WorkstationJewelcrafting,
            DataPropModel.WorkstationPrayer => ContractPropModel.WorkstationPrayer,
            DataPropModel.WorkstationReading => ContractPropModel.WorkstationReading,
            DataPropModel.WorkstationTailoring => ContractPropModel.WorkstationTailoring,
            DataPropModel.WorkstationTrade => ContractPropModel.WorkstationTrade,
            DataPropModel.WorkstationWeaponsmithing => ContractPropModel.WorkstationWeaponsmithing,
        };
}
