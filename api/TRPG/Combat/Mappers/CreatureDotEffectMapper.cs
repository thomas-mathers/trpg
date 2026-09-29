using TRPG.Application.Creatures.Results;
using ContractActiveDot = TRPG.Combat.Responses.ActiveDot;

namespace TRPG.Combat.Mappers;

internal static class CreatureDotEffectMapper
{
    public static ContractActiveDot ToContract(this CreatureDotEffect dot) =>
        new(
            dot.AbilityName,
            dot.Amount,
            dot.DamageType.ToContract(),
            dot.ExpiresAt.ToGameTimeMilliseconds()
        );
}
