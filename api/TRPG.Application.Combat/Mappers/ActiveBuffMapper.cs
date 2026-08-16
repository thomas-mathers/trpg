using TRPG.Application.CreatureFormulas;
using ContractActiveBuff = TRPG.Application.Combat.ClientEvents.ActiveBuff;

namespace TRPG.Application.Combat.Mappers;

internal static class ActiveBuffMapper
{
    public static ContractActiveBuff ToContract(this ActiveBuff buff) =>
        new(
            buff.AbilityName,
            buff.Attribute.ToContract(),
            buff.Amount,
            buff.AmountType.ToContract(),
            buff.RemainingTurns
        );
}
