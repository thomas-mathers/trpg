using ContractCombatSpeedType = TRPG.Contracts.Inventory.Responses.CombatSpeedType;
using DataCombatSpeedType = TRPG.Domain.Models.CombatSpeedType;

namespace TRPG.Creatures.Mappers;

internal static class CombatSpeedTypeMapper
{
    public static ContractCombatSpeedType ToContract(this DataCombatSpeedType type) =>
        type switch
        {
            DataCombatSpeedType.IncreasedAttackSpeed =>
                ContractCombatSpeedType.IncreasedAttackSpeed,
            DataCombatSpeedType.FasterCastRate => ContractCombatSpeedType.FasterCastRate,
            DataCombatSpeedType.FasterHitRecovery => ContractCombatSpeedType.FasterHitRecovery,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
}
