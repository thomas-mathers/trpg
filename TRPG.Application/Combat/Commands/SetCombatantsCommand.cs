using Microsoft.EntityFrameworkCore;
using TRPG.Application.Game;

namespace TRPG.Application.Combat.Commands;

internal class SetCombatantsCommand
{
    public required GameSessionLock Lock { get; init; }
    public required IReadOnlyList<Combatant> Combatants { get; init; }
}

internal class SetCombatantsCommandHandler
{
    public async Task Handle(
        SetCombatantsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = GameSessionDbContextFactory.Create(command.Lock.Connection);
        await context
            .Combatants.Where(c => c.SessionId == command.Lock.SessionId)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var combatant in command.Combatants)
        {
            context.Combatants.Add(CombatantMapper.ToRow(combatant, command.Lock.SessionId));
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
