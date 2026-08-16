using TRPG.Application.Combat;
using ContractActiveHot = TRPG.Combat.ClientModels.ActiveHot;

namespace TRPG.Combat.Mappers;

internal static class ActiveHotMapper
{
    public static ContractActiveHot ToContract(this ActiveHot hot) =>
        new(hot.AbilityName, hot.Amount, hot.RemainingTurns);
}
