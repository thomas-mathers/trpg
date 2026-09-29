using TRPG.Domain;
using ConditionType = TRPG.Application.Abilities.ConditionType;
using ContractActiveConditions = TRPG.Combat.Responses.ActiveConditions;

namespace TRPG.Combat.Mappers;

internal static class ActiveConditionsMapper
{
    public static ContractActiveConditions ToContract(
        this IReadOnlyDictionary<ConditionType, GameInstant> conditions
    ) =>
        new()
        {
            Blinded = conditions.ExpiryOf(ConditionType.Blinded),
            Bleeding = conditions.ExpiryOf(ConditionType.Bleeding),
            Burning = conditions.ExpiryOf(ConditionType.Burning),
            Disarmed = conditions.ExpiryOf(ConditionType.Disarmed),
            Frozen = conditions.ExpiryOf(ConditionType.Frozen),
            Poisoned = conditions.ExpiryOf(ConditionType.Poisoned),
            Silenced = conditions.ExpiryOf(ConditionType.Silenced),
            Snared = conditions.ExpiryOf(ConditionType.Snared),
            Stunned = conditions.ExpiryOf(ConditionType.Stunned),
        };

    private static long? ExpiryOf(
        this IReadOnlyDictionary<ConditionType, GameInstant> conditions,
        ConditionType condition
    ) =>
        conditions.TryGetValue(condition, out var expiresAt)
            ? expiresAt.ToGameTimeMilliseconds()
            : null;
}
