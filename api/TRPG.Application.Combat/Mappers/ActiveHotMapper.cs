using TRPG.Application.Combat;
using ContractActiveHot = TRPG.Application.Combat.Responses.ActiveHot;

namespace TRPG.Application.Combat.Mappers;

internal static class ActiveHotMapper
{
    public static ContractActiveHot ToContract(this ActiveHot hot) =>
        new(hot.AbilityName, hot.Amount, hot.RemainingTurns);
}
