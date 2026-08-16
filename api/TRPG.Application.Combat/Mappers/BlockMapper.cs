using TRPG.Application.Combat.Events;
using ContractEvent = TRPG.Contracts.Combat.Responses.CombatBlockEvent;

namespace TRPG.Application.Combat.Mappers;

internal static class BlockMapper
{
    public static ContractEvent ToContract(this Block block) =>
        new(
            block.AttackerId,
            block.AttackerName,
            block.AbilityName,
            block.TargetId,
            block.TargetName
        )
        {
            Narration = CombatNarration.Describe(block),
        };
}
