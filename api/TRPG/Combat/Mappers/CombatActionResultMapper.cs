using TRPG.Application.Combat;
using TRPG.Application.Combat.Events;
using TRPG.Combat.Responses;
using TRPG.Domain.Models;

namespace TRPG.Combat.Mappers;

internal static class CombatActionResultMapper
{
    public static IReadOnlyList<CombatActionResult> ToCombatActionResults(
        this IReadOnlyList<CombatResolution> resolutions
    ) =>
        resolutions
            .Select(resolution =>
                resolution switch
                {
                    Hit hit => hit.ToCombatActionResult(),
                    Miss miss => miss.ToCombatActionResult(),
                    Block block => block.ToCombatActionResult(),
                    Healed healed => healed.ToCombatActionResult(),
                    HealOverTimeApplied hot => hot.ToCombatActionResult(),
                    BuffApplied buff => buff.ToCombatActionResult(),
                    ConsumedPotion potion => potion.ToCombatActionResult(),
                    FleeFailed fleeFailed => fleeFailed.ToCombatActionResult(),
                    _ => null,
                }
            )
            .OfType<CombatActionResult>()
            .ToArray();

    private static CombatActionResult ToCombatActionResult(this Hit hit) =>
        new(
            CombatActionOutcome.Hit,
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
            hit.AppliedConditions.Select(condition => condition.ToContract()).ToArray(),
            null,
            null,
            null,
            CombatNarration.Describe(hit) ?? string.Empty
        );

    private static CombatActionResult ToCombatActionResult(this Miss miss) =>
        new(
            CombatActionOutcome.Miss,
            miss.AttackerId,
            miss.AttackerName,
            miss.AbilityName,
            miss.TargetId,
            miss.TargetName,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            CombatNarration.Describe(miss) ?? string.Empty
        );

    private static CombatActionResult ToCombatActionResult(this Block block) =>
        new(
            CombatActionOutcome.Block,
            block.AttackerId,
            block.AttackerName,
            block.AbilityName,
            block.TargetId,
            block.TargetName,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            CombatNarration.Describe(block) ?? string.Empty
        );

    private static CombatActionResult ToCombatActionResult(this Healed healed) =>
        new(
            CombatActionOutcome.Heal,
            healed.SourceId,
            healed.SourceName,
            healed.AbilityName,
            healed.TargetId,
            healed.TargetName,
            null,
            null,
            null,
            false,
            healed.TargetRemainingHp,
            healed.TargetMaximumHp,
            null,
            null,
            null,
            null,
            CombatNarration.Describe(healed) ?? string.Empty
        );

    private static CombatActionResult ToCombatActionResult(this HealOverTimeApplied hot) =>
        new(
            CombatActionOutcome.HealOverTime,
            hot.SourceId,
            hot.SourceName,
            hot.AbilityName,
            hot.TargetId,
            hot.TargetName,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            hot.AmountPerTurn,
            hot.Duration,
            CombatNarration.Describe(hot) ?? string.Empty
        );

    private static CombatActionResult ToCombatActionResult(this BuffApplied buff) =>
        new(
            CombatActionOutcome.Buff,
            buff.SourceId,
            buff.SourceName,
            buff.AbilityName,
            buff.TargetId,
            buff.TargetName,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            buff.AppliedModifiers.Select(modifier => new CombatActionBuffModifier(
                    modifier.Attribute.ToContract(),
                    modifier.Amount,
                    modifier.AmountType.ToContract(),
                    modifier.RemainingTurns
                ))
                .ToArray(),
            null,
            null,
            CombatNarration.Describe(buff) ?? string.Empty
        );

    private static CombatActionResult ToCombatActionResult(this ConsumedPotion potion) =>
        new(
            CombatActionOutcome.ConsumePotion,
            potion.CreatureId,
            potion.CreatureName,
            potion.ItemName,
            potion.CreatureId,
            potion.CreatureName,
            null,
            null,
            null,
            false,
            potion.Resource == ResourceType.Hp ? potion.RemainingValue : null,
            potion.Resource == ResourceType.Hp ? potion.MaximumValue : null,
            null,
            null,
            null,
            null,
            CombatNarration.Describe(potion) ?? string.Empty
        );

    private static CombatActionResult ToCombatActionResult(this FleeFailed fleeFailed) =>
        new(
            CombatActionOutcome.FleeFailed,
            fleeFailed.CreatureId,
            fleeFailed.CreatureName,
            "Flee",
            fleeFailed.CreatureId,
            fleeFailed.CreatureName,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            CombatNarration.Describe(fleeFailed) ?? string.Empty
        );
}
