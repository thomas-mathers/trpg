using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Effects;
using TRPG.Application.Effects.Mappers;

namespace TRPG.Application.Combat.Mappers;

public static class CombatantMapper
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

    internal static EffectState ToEffectState(this Combatant combatant) =>
        new()
        {
            Attributes = combatant.Attributes,
            EquippedItems = combatant.EquippedItems,
            CurrentHp = combatant.CurrentHp,
            ActiveConditions = combatant.ActiveConditions,
            CooldownReadyAtByAbility = combatant.CooldownReadyAtByAbility,
            ActiveDots = combatant.ActiveDots,
            ActiveHots = combatant.ActiveHots,
            ActiveBuffs = combatant.ActiveBuffs,
        };
}
