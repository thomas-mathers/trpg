using ContractSpecialHitType = TRPG.Inventory.Responses.SpecialHitType;
using DataSpecialHitType = TRPG.Domain.Models.SpecialHitType;

namespace TRPG.Creatures.Mappers;

internal static class SpecialHitTypeMapper
{
    public static ContractSpecialHitType ToResponse(this DataSpecialHitType type) =>
        type switch
        {
            DataSpecialHitType.CrushingBlow => ContractSpecialHitType.CrushingBlow,
            DataSpecialHitType.DeadlyStrike => ContractSpecialHitType.DeadlyStrike,
            DataSpecialHitType.OpenWounds => ContractSpecialHitType.OpenWounds,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
}
