using AbilitiesConditionType = TRPG.Application.Abilities.ConditionType;
using ContractConditionType = TRPG.Combat.Responses.ConditionType;

namespace TRPG.Combat.Mappers;

internal static class ConditionTypeMapper
{
    public static ContractConditionType ToContract(this AbilitiesConditionType condition) =>
        condition switch
        {
            AbilitiesConditionType.Blinded => ContractConditionType.Blinded,
            AbilitiesConditionType.Bleeding => ContractConditionType.Bleeding,
            AbilitiesConditionType.Burning => ContractConditionType.Burning,
            AbilitiesConditionType.Disarmed => ContractConditionType.Disarmed,
            AbilitiesConditionType.Frozen => ContractConditionType.Frozen,
            AbilitiesConditionType.Poisoned => ContractConditionType.Poisoned,
            AbilitiesConditionType.Silenced => ContractConditionType.Silenced,
            AbilitiesConditionType.Snared => ContractConditionType.Snared,
            AbilitiesConditionType.Stunned => ContractConditionType.Stunned,
            _ => throw new ArgumentOutOfRangeException(nameof(condition), condition, null),
        };
}
