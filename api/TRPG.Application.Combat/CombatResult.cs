using TRPG.Application.Abilities;
using TRPG.Application.Combat.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat;

public record CombatResultPlayerState(
    string Name,
    int CurrentHp,
    int MaximumHp,
    IReadOnlyList<string> Abilities,
    IReadOnlyDictionary<ConditionType, int> ActiveConditions
);

public record CombatResultEnemyState(
    string Name,
    int CurrentHp,
    int MaximumHp,
    IReadOnlyDictionary<ConditionType, int> ActiveConditions
);

public record CombatResult(
    CombatOutcome Outcome,
    CombatResultPlayerState Player,
    IReadOnlyList<CombatResultEnemyState> Enemies,
    IReadOnlyList<CombatResolution> Events
);

internal static class CombatStateExtensions
{
    public static CombatResult ToCombatResult(this CombatState state)
    {
        var player = state.Combatants.Single(c => c.IsPlayer);
        var aliveEnemies = state.Combatants.Where(c => c is { IsPlayer: false, IsAlive: true });

        return new CombatResult(
            state.Outcome,
            new CombatResultPlayerState(
                player.Name,
                player.CurrentHp,
                player.MaximumHp,
                player.Abilities,
                player.ActiveConditions
            ),
            aliveEnemies
                .Select(enemy => new CombatResultEnemyState(
                    enemy.Name,
                    enemy.CurrentHp,
                    enemy.MaximumHp,
                    enemy.ActiveConditions
                ))
                .ToArray(),
            state.Events
        );
    }
}
