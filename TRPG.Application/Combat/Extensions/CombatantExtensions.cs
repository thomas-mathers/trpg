namespace TRPG.Application.Combat.Extensions;

public static class CombatantExtensions
{
    public static CombatantState ToCombatantState(this Combatant combatant)
    {
        return new CombatantState(
            Id: combatant.CreatureId,
            Name: combatant.Name,
            IsPlayer: combatant.IsPlayer,
            CurrentHp: combatant.CurrentHp,
            MaximumHp: combatant.MaximumHp,
            CurrentAp: combatant.CurrentAp,
            CurrentMp: combatant.CurrentMp,
            IsAlive: combatant.IsAlive,
            Abilities: combatant.Abilities.Select(a => a.Name).ToArray(),
            ActiveConditions: combatant.ActiveConditions.Where(c => c.Value > 0).ToDictionary(),
            ItemsUsedCounts: combatant.ItemsUsedCounts
        );
    }
}
