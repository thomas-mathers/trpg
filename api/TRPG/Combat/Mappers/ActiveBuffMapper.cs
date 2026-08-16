using TRPG.Application.Combat.Results;
using ContractActiveBuff = TRPG.Combat.ClientModels.ActiveBuff;

namespace TRPG.Combat.Mappers;

internal static class ActiveBuffMapper
{
    public static ContractActiveBuff ToContract(this CombatBuffState buff) =>
        new(
            buff.AbilityName,
            buff.Attribute.ToContract(),
            buff.Amount,
            buff.AmountType.ToContract(),
            buff.RemainingTurns
        );
}
