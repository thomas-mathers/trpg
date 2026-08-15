using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;

namespace TRPG.Application.Combat.Commands;

internal class PersistCombatantsCommand
{
    public required IReadOnlyList<Combatant> Combatants { get; init; }
}

internal class PersistCombatantsCommandHandler(TrpgDbContext context)
    : ICommandHandler<PersistCombatantsCommand>
{
    public async Task Handle(
        PersistCombatantsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var ids = command.Combatants.Select(c => c.CreatureId).ToArray();
        var creatures = await context
            .Creatures.Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        foreach (var combatant in command.Combatants)
        {
            combatant.ApplyTo(creatures[combatant.CreatureId]);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
