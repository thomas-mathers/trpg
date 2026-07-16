using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Commands;

internal class PersistCombatantResourcesCommand
{
    public required IReadOnlyList<CombatantState> Combatants { get; init; }
    public required TimeSpan Playtime { get; init; }
}

internal class PersistCombatantResourcesCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        PersistCombatantResourcesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var ids = command.Combatants.Select(c => c.Id).ToArray();
        var creatures = await context
            .Creatures.Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        foreach (var combatant in command.Combatants)
        {
            var creature = creatures[combatant.Id];
            creature.CurrentHp = combatant.CurrentHp;
            creature.CurrentAp = combatant.CurrentAp;
            creature.CurrentMp = combatant.CurrentMp;

            if (!combatant.IsAlive)
            {
                creature.State = CreatureState.Dead;
            }
            else
            {
                creature.LastRegenPlaytime = command.Playtime;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
