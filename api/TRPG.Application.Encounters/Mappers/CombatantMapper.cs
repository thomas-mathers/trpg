using TRPG.Application.Combat;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Results;
using PersistedCombat = TRPG.Domain.Models;

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
            combatant.CooldownReadyAtByAbility,
            combatant
                .ActiveDots.Select(dot => new PersistedCombat.ActiveDot
                {
                    AbilityName = dot.AbilityName,
                    Amount = dot.Amount,
                    DamageType = dot.DamageType.ToString(),
                    NextTickAt = dot.NextTickAt,
                    ExpiresAt = dot.ExpiresAt,
                })
                .ToArray(),
            combatant
                .ActiveHots.Select(hot => new PersistedCombat.ActiveHot
                {
                    AbilityName = hot.AbilityName,
                    Amount = hot.Amount,
                    NextTickAt = hot.NextTickAt,
                    ExpiresAt = hot.ExpiresAt,
                })
                .ToArray(),
            combatant
                .ActiveBuffs.Select(buff => new PersistedCombat.ActiveBuff
                {
                    AbilityName = buff.AbilityName,
                    Amount = buff.Amount,
                    Attribute = buff.Attribute.ToString(),
                    ExpiresAt = buff.ExpiresAt,
                    AmountType = buff.AmountType.ToString(),
                })
                .ToArray()
        );
}
