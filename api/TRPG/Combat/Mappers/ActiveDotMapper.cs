using TRPG.Application.Combat;
using ContractActiveDot = TRPG.Combat.ClientModels.ActiveDot;

namespace TRPG.Combat.Mappers;

internal static class ActiveDotMapper
{
    public static ContractActiveDot ToContract(this ActiveDot dot) =>
        new(dot.AbilityName, dot.Amount, dot.DamageType.ToContract(), dot.RemainingTurns);
}
