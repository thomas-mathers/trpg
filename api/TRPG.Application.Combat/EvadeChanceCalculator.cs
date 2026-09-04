using TRPG.Application.Configuration;

namespace TRPG.Application.Combat;

public record EvadeParticipant(
    float Dexterity,
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int MaximumAp
);

public static class EvadeChanceCalculator
{
    public static float CatchChance(
        FleeOptions options,
        EvadeParticipant defender,
        IReadOnlyCollection<EvadeParticipant> chasers
    )
    {
        if (chasers.Count == 0)
        {
            return options.MinimumCatchChance;
        }

        var effectiveDefenderDexterity = EffectiveDexterity(defender);
        var effectiveChaserDexterity = chasers.Max(EffectiveDexterity);

        return Math.Clamp(
            effectiveChaserDexterity
                / Math.Max(effectiveDefenderDexterity, 1f)
                * options.CatchChanceMultiplier,
            options.MinimumCatchChance,
            options.MaximumCatchChance
        );
    }

    private static float EffectiveDexterity(EvadeParticipant participant) =>
        participant.Dexterity * ConditionFactor(participant);

    private static float ConditionFactor(EvadeParticipant participant)
    {
        var hpPercent =
            participant.MaximumHp > 0 ? (float)participant.CurrentHp / participant.MaximumHp : 0f;
        var apPercent =
            participant.MaximumAp > 0 ? (float)participant.CurrentAp / participant.MaximumAp : 0f;

        return Math.Min(hpPercent, apPercent);
    }
}
