using TRPG.Application.Combat;
using ContractActiveDot = TRPG.Application.Combat.Responses.ActiveDot;

namespace TRPG.Application.Combat.Mappers;

internal static class ActiveDotMapper
{
    public static ContractActiveDot ToContract(this ActiveDot dot) =>
        new(dot.AbilityName, dot.Amount, dot.DamageType.ToContract(), dot.RemainingTurns);
}
