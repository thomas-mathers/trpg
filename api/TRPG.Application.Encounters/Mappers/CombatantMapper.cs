using TRPG.Application.Combat;
using TRPG.Application.Creatures.Commands;
using PersistedCombat = TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Mappers;

internal static class CombatantMapper
{
    public static CreatureCombatStateUpdate ToCreatureCombatStateUpdate(this Combatant combatant) =>
        new(
            combatant.CreatureId,
            combatant.CurrentHp,
            combatant.CurrentAp,
            combatant.CurrentMp,
            combatant.IsAlive,
            combatant.ActiveConditions.ToDictionary(
                condition => condition.Key.ToString(),
                condition => condition.Value
            ),
            combatant.CooldownRemainingByAbility,
            combatant
                .ActiveDots.Select(dot => new PersistedCombat.ActiveDot
                {
                    AbilityName = dot.AbilityName,
                    Amount = dot.Amount,
                    DamageType = dot.DamageType.ToString(),
                    RemainingTurns = dot.RemainingTurns,
                })
                .ToArray(),
            combatant
                .ActiveHots.Select(hot => new PersistedCombat.ActiveHot
                {
                    AbilityName = hot.AbilityName,
                    Amount = hot.Amount,
                    RemainingTurns = hot.RemainingTurns,
                })
                .ToArray(),
            combatant
                .ActiveBuffs.Select(buff => new PersistedCombat.ActiveBuff
                {
                    AbilityName = buff.AbilityName,
                    Amount = buff.Amount,
                    Attribute = buff.Attribute.ToString(),
                    RemainingTurns = buff.RemainingTurns,
                    AmountType = buff.AmountType.ToString(),
                })
                .ToArray()
        );
}
