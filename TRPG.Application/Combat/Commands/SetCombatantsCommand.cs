using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Combat.Commands;

internal class SetCombatantsCommand
{
    public required Guid SessionId { get; init; }
    public required IReadOnlyList<Combatant> Combatants { get; init; }
}

internal class SetCombatantsCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        SetCombatantsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await context
            .Combatants.Where(c => c.SessionId == command.SessionId)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var combatant in command.Combatants)
        {
            context.Combatants.Add(CombatantMapper.ToRow(combatant, command.SessionId));
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
