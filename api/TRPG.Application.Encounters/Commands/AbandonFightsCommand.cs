using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class AbandonFightsCommand
{
    public required Guid WorldId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class AbandonFightsCommandHandler(
    IEncountersDbContext context,
    EncounterEngagementManager engagementManager
) : ICommandHandler<AbandonFightsCommand>
{
    public async Task Handle(
        AbandonFightsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var fights = await context
            .Encounters.OfType<FightEncounter>()
            .Where(fight =>
                fight.WorldId == command.WorldId && fight.State == EncounterState.Active
            )
            .ToArrayAsync(cancellationToken);

        foreach (var fight in fights)
        {
            fight.CompletedAt = DateTime.UtcNow;
            fight.CompletedAtGameTime = command.GameTime;
            fight.State = EncounterState.Completed;
            fight.Outcome = CombatOutcome.Fled;
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var fight in fights)
        {
            await engagementManager.ReconcileResolved(fight, command.GameTime, cancellationToken);
        }

        transaction.Complete();
    }
}
