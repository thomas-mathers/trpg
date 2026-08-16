using TRPG.Application.Combat.Results;
using ContractActiveDot = TRPG.Combat.ClientModels.ActiveDot;

namespace TRPG.Combat.Mappers;

internal static class ActiveDotMapper
{
    public static ContractActiveDot ToContract(this CombatDotState dot) =>
        new(dot.AbilityName, dot.Amount, dot.DamageType.ToContract(), dot.RemainingTurns);
}
