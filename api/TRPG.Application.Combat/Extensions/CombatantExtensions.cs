using TRPG.Application.Combat.Results;

namespace TRPG.Application.Combat.Extensions;

public static class CombatantExtensions
{
    public static CombatantResult ToCombatantResult(this Combatant combatant)
    {
        return new CombatantResult(
            Id: combatant.CreatureId,
            Name: combatant.Name,
            Level: combatant.Level,
            IsPlayer: combatant.IsPlayer,
            CurrentHp: combatant.CurrentHp,
            MaximumHp: combatant.MaximumHp,
            CurrentAp: combatant.CurrentAp,
            MaximumAp: combatant.MaximumAp,
            CurrentMp: combatant.CurrentMp,
            MaximumMp: combatant.MaximumMp,
            IsAlive: combatant.IsAlive,
            Abilities: combatant.Abilities.Select(a => a.Name).ToArray(),
            ActiveConditions: combatant.ActiveConditions.Where(c => c.Value > 0).ToDictionary(),
            ActiveDots: combatant
                .ActiveDots.Select(dot => new CombatDotState(
                    dot.AbilityName,
                    dot.Amount,
                    dot.DamageType,
                    dot.RemainingTurns
                ))
                .ToArray(),
            ActiveHots: combatant
                .ActiveHots.Select(hot => new CombatHotState(
                    hot.AbilityName,
                    hot.Amount,
                    hot.RemainingTurns
                ))
                .ToArray(),
            ActiveBuffs: combatant
                .ActiveBuffs.Select(buff => new CombatBuffState(
                    buff.AbilityName,
                    buff.Attribute,
                    buff.Amount,
                    buff.AmountType,
                    buff.RemainingTurns
                ))
                .ToArray(),
            ItemsUsedCounts: combatant.ItemsUsedCounts.ToDictionary()
        );
    }
}
