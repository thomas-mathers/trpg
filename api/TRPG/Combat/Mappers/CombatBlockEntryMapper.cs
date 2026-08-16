using TRPG.Application.Combat;
using TRPG.Application.Combat.Events;
using CombatBlockEntry = TRPG.Combat.ClientModels.CombatBlockEntry;

namespace TRPG.Combat.Mappers;

internal static class CombatBlockEntryMapper
{
    public static CombatBlockEntry ToContract(this Block block) =>
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
