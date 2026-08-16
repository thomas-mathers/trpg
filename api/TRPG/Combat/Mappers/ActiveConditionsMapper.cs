using ConditionType = TRPG.Application.Abilities.ConditionType;
using ContractActiveConditions = TRPG.Combat.ClientModels.ActiveConditions;

namespace TRPG.Combat.Mappers;

internal static class ActiveConditionsMapper
{
    public static ContractActiveConditions ToContract(
        this IReadOnlyDictionary<ConditionType, int> conditions
    ) =>
        new()
        {
            Blinded = conditions.GetValueOrDefault(ConditionType.Blinded),
            Bleeding = conditions.GetValueOrDefault(ConditionType.Bleeding),
            Burning = conditions.GetValueOrDefault(ConditionType.Burning),
            Disarmed = conditions.GetValueOrDefault(ConditionType.Disarmed),
            Frozen = conditions.GetValueOrDefault(ConditionType.Frozen),
            Poisoned = conditions.GetValueOrDefault(ConditionType.Poisoned),
            Silenced = conditions.GetValueOrDefault(ConditionType.Silenced),
            Snared = conditions.GetValueOrDefault(ConditionType.Snared),
            Stunned = conditions.GetValueOrDefault(ConditionType.Stunned),
        };
}
