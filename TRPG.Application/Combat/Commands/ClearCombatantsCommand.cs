using Microsoft.EntityFrameworkCore;
using TRPG.Application.Game;

namespace TRPG.Application.Combat.Commands;

internal class ClearCombatantsCommand
{
    public required GameSessionLock Lock { get; init; }
}

internal class ClearCombatantsCommandHandler
{
    public async Task Handle(
        ClearCombatantsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = GameSessionDbContextFactory.Create(command.Lock.Connection);
        await context
            .Combatants.Where(c => c.SessionId == command.Lock.SessionId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
