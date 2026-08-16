using TRPG.Application.Combat.Events;
using ContractEvent = TRPG.Contracts.Combat.Responses.CombatMissEvent;

namespace TRPG.Application.Combat.Mappers;

internal static class MissMapper
{
    public static ContractEvent ToContract(this Miss miss) =>
        new(miss.AttackerId, miss.AttackerName, miss.AbilityName, miss.TargetId, miss.TargetName)
        {
            Narration = CombatNarration.Describe(miss),
        };
}
