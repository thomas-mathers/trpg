using TRPG.Application.Abilities;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Extensions;

public static class CombatantExtensions
{
    public static CombatantState ToCombatantState(this Combatant combatant)
    {
        return new CombatantState(
            combatant.CreatureId,
            combatant.Name,
            combatant.IsPlayer,
            combatant.CurrentHp,
            combatant.MaximumHp,
            combatant.IsAlive,
            combatant.Abilities.Select(a => a.Name).ToArray(),
            combatant.ActiveConditions.Where(c => c.Value > 0).ToDictionary()
        );
    }
}
