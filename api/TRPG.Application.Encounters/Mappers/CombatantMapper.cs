using TRPG.Application.Combat;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Effects.Mappers;

namespace TRPG.Application.Encounters.Mappers;

internal static class CombatantMapper
{
    public static CreatureVitals ToVitals(this Combatant combatant) =>
        new(
            combatant.CreatureId,
            combatant.CurrentHp,
            combatant.MaximumHp,
            combatant.CurrentAp,
            combatant.MaximumAp,
            combatant.CurrentMp,
            combatant.MaximumMp
        );

    public static CreatureStateUpdate ToCreatureStateUpdate(this Combatant combatant) =>
        new(
            CreatureId: combatant.CreatureId,
            CurrentHp: combatant.CurrentHp,
            CurrentAp: combatant.CurrentAp,
            CurrentMp: combatant.CurrentMp,
            IsAlive: combatant.IsAlive,
            ActiveConditions: combatant.ActiveConditions.ToDictionary(
                condition => condition.Key.ToString(),
                condition => condition.Value
            ),
            CooldownReadyAtByAbility: combatant.CooldownReadyAtByAbility,
            ActiveDots: combatant.ActiveDots.Select(effect => effect.ToModel()).ToArray(),
            ActiveHots: combatant.ActiveHots.Select(effect => effect.ToModel()).ToArray(),
            ActiveBuffs: combatant.ActiveBuffs.Select(effect => effect.ToModel()).ToArray()
        );
}
