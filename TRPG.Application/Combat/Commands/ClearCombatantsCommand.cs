using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Combat.Commands;

internal class ClearCombatantsCommand
{
    public required Guid SessionId { get; init; }
}

internal class ClearCombatantsCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        ClearCombatantsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Combatants.Where(c => c.SessionId == command.SessionId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
