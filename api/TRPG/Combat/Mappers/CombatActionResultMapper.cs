using TRPG.Application.Combat;
using TRPG.Application.Combat.Events;
using TRPG.Combat.Responses;

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
            CombatNarration.Describe(block) ?? string.Empty
        );
}
