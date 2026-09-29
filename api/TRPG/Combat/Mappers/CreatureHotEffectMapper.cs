using TRPG.Application.Creatures.Results;
using ContractActiveHot = TRPG.Combat.Responses.ActiveHot;

namespace TRPG.Combat.Mappers;

internal static class CreatureHotEffectMapper
{
    public static ContractActiveHot ToContract(this CreatureHotEffect hot) =>
        new(hot.AbilityName, hot.Amount, hot.ExpiresAt.ToGameTimeMilliseconds());
}
