using TRPG.Application.Combat;
using TRPG.Application.Combat.Events;
using CombatHitEntry = TRPG.Combat.ClientModels.CombatHitEntry;

namespace TRPG.Combat.Mappers;

internal static class CombatHitEntryMapper
{
    public static CombatHitEntry ToContract(this Hit hit) =>
        new(
            hit.AttackerId,
            hit.AttackerName,
            hit.AbilityName,
            hit.TargetId,
            hit.TargetName,
            hit.Damage,
            hit.DamageType.ToContract(),
            hit.IsCritical,
            hit.Killed,
            hit.TargetRemainingHp,
            hit.TargetMaximumHp,
            hit.AppliedConditions.Select(condition => condition.ToContract()).ToArray()
        )
        {
            Narration = CombatNarration.Describe(hit),
        };
}
