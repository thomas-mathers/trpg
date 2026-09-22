using TRPG.Application.Configuration;

namespace TRPG.Application.Encounters;

internal record IntimidationParticipant(
    int Strength,
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int MaximumAp
);

internal static class IntimidationChanceCalculator
{
    public static float SuccessChance(
        IntimidationOptions options,
        IntimidationParticipant intimidator,
        IReadOnlyCollection<IntimidationParticipant> targets
    )
    {
        if (targets.Count == 0)
        {
            return options.MaximumSuccessChance;
        }

        var effectiveIntimidatorStrength = EffectiveStrength(intimidator);
        var effectiveTargetStrength = targets.Max(EffectiveStrength);

        return Math.Clamp(
            effectiveIntimidatorStrength
                / Math.Max(effectiveTargetStrength, 1f)
                * options.SuccessChanceMultiplier,
            options.MinimumSuccessChance,
            options.MaximumSuccessChance
        );
    }

    private static float EffectiveStrength(IntimidationParticipant participant) =>
        participant.Strength * ConditionFactor(participant);

    private static float ConditionFactor(IntimidationParticipant participant)
    {
        var hpPercent =
            participant.MaximumHp > 0 ? (float)participant.CurrentHp / participant.MaximumHp : 0f;
        var apPercent =
            participant.MaximumAp > 0 ? (float)participant.CurrentAp / participant.MaximumAp : 0f;

        return Math.Min(hpPercent, apPercent);
    }
}
