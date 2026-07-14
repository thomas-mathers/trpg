using TRPG.Application.Abilities;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Extensions;

public static class CombatantExtensions
{
    public static EnemyCombatState ToEnemyCombatState(this Combatant enemy)
    {
        return new EnemyCombatState(
            enemy.Name,
            enemy.CurrentHp,
            enemy.MaximumHp,
            enemy.ActiveConditions.Where(c => c.Value > 0).ToDictionary()
        );
    }

    public static PlayerCombatState ToPlayerCombatState(this Combatant player)
    {
        return new PlayerCombatState(
            player.Name,
            player.CurrentHp,
            player.MaximumHp,
            player.Abilities.Select(a => a.Name).ToArray(),
            player.ActiveConditions.Where(c => c.Value > 0).ToDictionary()
        );
    }
}
