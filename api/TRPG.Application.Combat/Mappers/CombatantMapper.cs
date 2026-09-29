using TRPG.Application.Effects;

namespace TRPG.Application.Combat.Mappers;

internal static class CombatantMapper
{
    public static EffectState ToEffectState(this Combatant combatant) =>
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
