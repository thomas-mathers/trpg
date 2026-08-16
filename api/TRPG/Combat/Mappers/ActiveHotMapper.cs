using TRPG.Application.Combat.Results;
using ContractActiveHot = TRPG.Combat.ClientModels.ActiveHot;

namespace TRPG.Combat.Mappers;

internal static class ActiveHotMapper
{
    public static ContractActiveHot ToContract(this CombatHotState hot) =>
        new(hot.AbilityName, hot.Amount, hot.RemainingTurns);
}
