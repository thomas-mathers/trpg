using ContractDamageType = TRPG.Contracts.Combat.Responses.DamageType;
using DataDamageType = TRPG.Domain.Models.DamageType;

namespace TRPG.Creatures.Mappers;

internal static class DamageTypeMapper
{
    public static ContractDamageType ToContract(this DataDamageType type) =>
        type switch
        {
            DataDamageType.Physical => ContractDamageType.Physical,
            DataDamageType.Fire => ContractDamageType.Fire,
            DataDamageType.Ice => ContractDamageType.Ice,
            DataDamageType.Lightning => ContractDamageType.Lightning,
            DataDamageType.Poison => ContractDamageType.Poison,
            DataDamageType.Magic => ContractDamageType.Magic,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
}
