using TRPG.Application.Creatures.Results;
using ContractActiveBuff = TRPG.Combat.Responses.ActiveBuff;

namespace TRPG.Combat.Mappers;

internal static class CreatureBuffEffectMapper
{
    public static ContractActiveBuff ToContract(this CreatureBuffEffect buff) =>
        new(
            buff.AbilityName,
            buff.Attribute.ToContract(),
            buff.Amount,
            buff.AmountType.ToContract(),
            buff.ExpiresAt.ToGameTimeMilliseconds()
        );
}
