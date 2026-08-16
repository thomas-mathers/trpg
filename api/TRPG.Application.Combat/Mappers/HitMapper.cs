using TRPG.Application.Combat.Events;
using CombatHitEvent = TRPG.Application.Combat.Responses.CombatHitEvent;

namespace TRPG.Application.Combat.Mappers;

internal static class HitMapper
{
    public static CombatHitEvent ToContract(this Hit hit) =>
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
